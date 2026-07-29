using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Implemented by BoardView. Interface so ClientManager compiles before the renderer and is mockable in tests.
public interface IBoardRenderer
{
    void Initialise(ClientBoardCache board, BoardCoordinateMapper mapper, byte localPlayerId);
    void SetHighlights(IReadOnlyCollection<Vector2Int> cells);
    void SetHover(Vector2Int? cell);
}

// One per player (InputAuthority = that player). Mediator: owns the client stack and routes, holds no rules.
// Server->client is per-viewer projected diffs over RPC, never [Networked] board state.
public class ClientManager : NetworkBehaviour
{
    // Cells per diff RPC; keep payload conservative to stay below Fusion's reliable RPC byte limit after framing overhead.
    public const int MaxDiffsPerRpc = 24;
    
    [Networked] public byte PlayerId { get; private set; } // 1-based; 0 = no owner
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
    
    private ServerGameManager _server; // server-side only
    private ClientBoardCache _board; // client-side only
    private BoardCoordinateMapper _mapper;
    private PlayerActionController _actions;
    private IBoardRenderer _renderer;
    private InputHandler _inputHandler;
    private bool _clientReady;
    private bool _bootstrapConfigured;
    private bool _awaitingInitialBoard;
    private bool _inputWired;
    private byte _localPlayerId;
    private bool _hasBufferedTurnState;
    private bool _bufferedIsMyTurn;
    private int _bufferedRemainingBudget;
    private bool _initFinishedHandshakeSent;
    // Local mirror of all known player action payloads received from the authoritative turn broadcaster.
    private readonly Dictionary<int, PlayerActionData> _playerActionsById = new Dictionary<int, PlayerActionData>();
    // Cached current-playing-player payload to support UI and turn-state consumers.
    private PlayerActionData _currentPlayingPlayer;
    
    // Diffs accumulate until the final chunk, so a multi-chunk blast is ONE cache update + recompute.
    private readonly List<CellDiff> _pendingDiffs = new(MaxDiffsPerRpc);
    
    // Client-side turn events raised from authoritative Server->InputAuthority RPC updates.
    public static event Action<PlayerActionData[]> PlayerActionsInitialised;
    public static event Action<PlayerActionData> CurrentPlayingPlayerChanged;
    public static event Action<PlayerActionData> TurnChanged;

    // Server-side setup, called by ServerGameManager right after spawn.
    public void InstantiateClientManager(ServerGameManager server, byte playerId)
    {
        Debug.Log("Attempting to instantiate a client manager...");
        
        if (!HasStateAuthority)
            return;
        
        _server = server ?? throw new ArgumentNullException(nameof(server));
        PlayerId = playerId;
        name = $"ClientManager_P{playerId}";

        Debug.Log($"Instantiated client manager at {name}.");
    }

    public void SetTraceLoggingEnabled(NetworkBool enabled)
    {
        if (!HasStateAuthority)
            return;

        TraceLogsEnabled = enabled;
    }
    
    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return; // someone else's ClientManager (incl. the server-side instance)

        var context = ClientSceneContext.Instance;
        if (!context)
        {
            Debug.LogError("[ClientManager] No ClientSceneContext in the scene; cannot initialise local client.");
            return;
        }

        _renderer = context.Renderer;
        _inputHandler = context.InputHandler;

        // Rig cached -> tell the server we're ready. The server replies with init + the full board once it exists.
        GameTraceLogger.Handshake(TraceLogsEnabled, $"Sending RPC_ClientReady from {name}.");
        RPC_ClientReady();
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_inputWired && _inputHandler)
        {
            _inputHandler.RequestSubmitted -= OnRequestSubmitted;
            _inputHandler.HoverChanged -= OnHoverChanged;
            _inputWired = false;
        }
        if (_actions != null)
            _actions.HighlightsInvalidated -= OnHighlightsInvalidated;
        if (_board != null)
            _board.Changed -= OnBoardChanged;

        _clientReady = false;
        _bootstrapConfigured = false;
        _awaitingInitialBoard = false;
        _inputWired = false;
        _hasBufferedTurnState = false;
        _initFinishedHandshakeSent = false;
        _server = null;
        _playerActionsById.Clear();
        _pendingDiffs.Clear();
    }
    
    #region Client -> Server
    // Readiness handshake: decouples client init from server spawn ordering (no scene-context race).
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ClientReady()
    {
        GameTraceLogger.Handshake(TraceLogsEnabled, $"Server received RPC_ClientReady from {name} (P{PlayerId}).");

        if (!_server)
        {
            Debug.LogError("[ClientManager] RPC_ClientReady on a peer with no ServerGameManager.");
            return;
        }
        // Hands readiness back to server so it can initialise this client and stream the full board diff.
        _server.OnClientReady(this);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ClientInitFinished()
    {
        GameTraceLogger.Handshake(TraceLogsEnabled, $"Server received RPC_ClientInitFinished from {name} (P{PlayerId}).");

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_ClientInitFinished on a non-authoritative peer.");
            return;
        }

        if (!_server)
        {
            Debug.LogError("[ClientManager] RPC_ClientInitFinished on a peer with no ServerGameManager.");
            return;
        }

        _server.OnClientInitFinished(this);
    }
    
    // Carries an INTENT, not a target type: move-into-empty and capture both yield Soldier, and BuildBase carries a window origin.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_RequestMove(Vector2Int cell, MoveIntent intent = MoveIntent.MoveSoldier)
    {
        GameTraceLogger.Move(TraceLogsEnabled, $"RPC_RequestMove from {name}: intent={intent}, cell={cell}.");

        if (!HasStateAuthority)
        {
            Debug.LogError("[ClientManager] RPC_RequestMove on a non-authoritative peer.");
            return;
        }
        
        if (!_server)
        {
            Debug.LogError("[ClientManager] RPC_RequestMove on a peer with no ServerGameManager.");
            return;
        }
        
        _server.HandleMoveRequest(this, cell, intent); // authoritative path; client-side filtering is UX only
    }
    
    #endregion
    
    #region Server -> This Client
    // One-shot bootstrap. GDD rules out reconnect/late-join, so a single init + diff stream is enough.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_InitialiseClient(byte playerId, short width, short height)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_InitialiseClient for {name} (P{playerId}) {width}x{height}.");

        if (_clientReady || _bootstrapConfigured)
        {
            Debug.LogWarning("[ClientManager] Duplicate RPC_InitialiseClient ignored.");
            return;
        }

        var context = ClientSceneContext.Instance;
        if (!context || !_inputHandler)
        {
            Debug.LogError("[ClientManager] Scene context missing at init time.");
            return;
        }

        _mapper = new BoardCoordinateMapper(context.Grid, context.BoardCamera, context.BoardOriginCell, width, height);
        _board = new ClientBoardCache(width, height);

        var legal = new LegalMoveCalculator(_board, playerId);
        var scanner = new BaseFormationScanner(_board, playerId);

        _actions = new PlayerActionController(
            new SoldierMoveMode(legal),
            new BombPlacementMode(legal),
            new BaseBuildMode(scanner));

        _board.Changed += OnBoardChanged;
        _actions.HighlightsInvalidated += OnHighlightsInvalidated;

        _bootstrapConfigured = true;
        _awaitingInitialBoard = true;
        _localPlayerId = playerId;
        _pendingDiffs.Clear();
    }

    // A chunk of this player's projected diff. Reliable + cumulative: a dropped chunk desyncs permanently.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ApplyDiffs(CellDiff[] diffs, int count, NetworkBool isFinalChunk)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_ApplyDiffs for {name} count={count}, final={isFinalChunk}.");

        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_ApplyDiffs on a non-input-authority peer.");
            return;
        }
        
        if (!_bootstrapConfigured)
        {
            Debug.LogWarning("[ClientManager] Diff before init; dropped.");
            return;
        }

        if (diffs != null)
        {
            var safe = Mathf.Clamp(count, 0, diffs.Length);
            for (var i = 0; i < safe; i++)
                _pendingDiffs.Add(diffs[i]);
        }
        if (!isFinalChunk)
            return;

        _board.Apply(_pendingDiffs); // raises Changed once
        _pendingDiffs.Clear();

        if (!_awaitingInitialBoard)
            return;

        _awaitingInitialBoard = false;
        FinalizeClientBootstrap();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_InitialisePlayerActions(PlayerActionData[] playerActions, int count)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_InitialisePlayerActions for {name} count={count}.");

        // Seeds the local mirror with the authoritative PlayerActionData snapshot for all players.
        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_InitialisePlayerActions on a non-input-authority peer.");
            return;
        }

        if (playerActions == null)
        {
            Debug.LogWarning("[ClientManager] RPC_InitialisePlayerActions called with null payload.");
            return;
        }

        var safe = Mathf.Clamp(count, 0, playerActions.Length);
        var payload = new PlayerActionData[safe];
        _playerActionsById.Clear();

        for (var i = 0; i < safe; i++)
        {
            var actionData = playerActions[i];
            payload[i] = actionData;
            _playerActionsById[actionData.PlayerId] = actionData;
        }

        PlayerActionsInitialised?.Invoke(payload);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_CurrentPlayingPlayerChanged(PlayerActionData currentPlayingPlayer)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_CurrentPlayingPlayerChanged for {name} playerId={currentPlayingPlayer.PlayerId}.");

        // Applies authoritative "current active player" updates during the active turn.
        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_CurrentPlayingPlayerChanged on a non-input-authority peer.");
            return;
        }

        _currentPlayingPlayer = currentPlayingPlayer;
        _playerActionsById[currentPlayingPlayer.PlayerId] = currentPlayingPlayer;
        CurrentPlayingPlayerChanged?.Invoke(currentPlayingPlayer);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_TurnChanged(PlayerActionData upcomingPlayer)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_TurnChanged for {name} upcomingPlayerId={upcomingPlayer.PlayerId}.");

        // Applies authoritative "next player turn started" updates at end-turn transition.
        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_TurnChanged on a non-input-authority peer.");
            return;
        }

        _currentPlayingPlayer = upcomingPlayer;
        _playerActionsById[upcomingPlayer.PlayerId] = upcomingPlayer;
        TurnChanged?.Invoke(upcomingPlayer);
    }
    #endregion

    private void OnRequestSubmitted(MoveRequest request)
    {
        GameTraceLogger.Move(TraceLogsEnabled, $"Local request submitted from {name}: intent={request.Intent}, cell={request.Cell}.");
        RPC_RequestMove(request.Cell, request.Intent);
    }
    private void OnBoardChanged(IReadOnlyList<CellDiff> _)
        => _actions.OnBoardChanged(); // renderer subscribes to _board.Changed itself
    private void OnHighlightsInvalidated()
        => _renderer?.SetHighlights(_actions.CurrentHighlights);
    private void OnHoverChanged(Vector2Int? cell)
        => _renderer?.SetHover(cell);

    private void FinalizeClientBootstrap()
    {
        Debug.Log("Finalizing client bootstrap.");
        if (_clientReady || !_bootstrapConfigured || _awaitingInitialBoard || !_inputHandler || _actions == null || _board == null)
            return;

        Debug.Log("Initializing input handler.");
        _inputHandler.Initialize(_mapper, _actions);
        _inputHandler.RequestSubmitted += OnRequestSubmitted;
        _inputHandler.HoverChanged += OnHoverChanged;
        _inputWired = true;

        _renderer?.Initialise(_board, _mapper, _localPlayerId);

        _clientReady = true;

        if (_hasBufferedTurnState)
        {
            _actions.SetTurnState(_bufferedIsMyTurn, _bufferedRemainingBudget);
            _hasBufferedTurnState = false;
        }

        if (!_initFinishedHandshakeSent && HasInputAuthority)
        {
            _initFinishedHandshakeSent = true;
            GameTraceLogger.Handshake(TraceLogsEnabled, $"Sending RPC_ClientInitFinished from {name}.");
            RPC_ClientInitFinished();
        }
    }
}
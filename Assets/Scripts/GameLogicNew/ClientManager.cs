using System;
using System.Collections.Generic;
using System.Linq;
using Events;
using Fusion;
using UnityEngine;
using Events;

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

    public static Action<string> OnPlayerTurnChanged;
    
    private ServerGameManager _server; // server-side only
    private ClientBoardCache _board; // client-side only
    private BoardCoordinateMapper _mapper;
    private PlayerActionController _actions;
    private IBoardRenderer _renderer;
    private InputHandler _inputHandler;
    private ClientConnectivityMap _connectivity;
    private BoardAudioInterpreter _audio;
    private BlastGenerationStamper _blastStamper;
    private bool _clientReady;
    private bool _inputWired;
    private bool _initialBoardApplied;
    private byte _localPlayerId;
    private bool _bufferedIsMyTurn;
    private int _bufferedRemainingBudget;
    [Networked] public NetworkBool IsReadyForBoardDiffs { get; private set; }
    // Local mirror of all known player action payloads received from the authoritative turn broadcaster.
    private readonly Dictionary<int, PlayerActionData> _playerActionsById = new Dictionary<int, PlayerActionData>();
    // Cached current-playing-player payload to support UI and turn-state consumers.
    
    // Diffs accumulate until the final chunk, so a multi-chunk blast is ONE cache update + recompute.
    private readonly List<CellDiff> _pendingDiffs = new(MaxDiffsPerRpc);

    private bool _isLoading = false;
    
    // Server-side setup, called by ServerGameManager right after spawn.
    public void InstantiateClientManager(ServerGameManager server, byte playerId, short  width, short height)
    {
        Debug.Log("Attempting to instantiate a client manager...");
        
        if (!HasStateAuthority)
            return;
        
        _server = server ?? throw new ArgumentNullException(nameof(server));
        PlayerId = playerId;
        name = $"ClientManager_P{playerId}";

        Debug.Log($"Instantiated client manager at {name}.");
        
        RPC_InitialiseClientOnClientSide(playerId, width, height);
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
        if (_connectivity != null)
        {
            _connectivity.Dispose();
            _connectivity = null;
        }

        _clientReady = false;
        _inputWired = false;
        _server = null;
        _playerActionsById.Clear();
        _pendingDiffs.Clear();
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
    
    #region Server -> This Client
    // One-shot bootstrap. GDD rules out reconnect/late-join, so a single init + diff stream is enough.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_InitialiseClientOnClientSide(byte playerId, short width, short height)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_InitialiseClient for {name} (P{playerId}) {width}x{height}.");
        
        if (_clientReady)
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
        
        EventBus.Raise(new ShowLoadingScreenEvent());
        _isLoading = true;
        Debug.Log("[ClientManager] Spawned loading screen.");
        
        _mapper = new BoardCoordinateMapper(context.Grid, context.BoardCamera, context.BoardOriginCell, width, height);
        _board = new ClientBoardCache(width, height);
        _audio = new BoardAudioInterpreter(_board, playerId);
        _blastStamper = new BlastGenerationStamper(_board);

        _connectivity = new ClientConnectivityMap(_board, playerId);
        var legal = new LegalMoveCalculator(_board, playerId, _connectivity);
        var scanner = new BaseFormationScanner(_board, playerId, _connectivity);
        
        _actions = new PlayerActionController(
            new SoldierMoveMode(legal),
            new BombPlacementMode(legal),
            new BaseBuildMode(scanner));
        
        _board.Changed += OnBoardChanged;
        _actions.HighlightsInvalidated += OnHighlightsInvalidated;
        
        _localPlayerId = playerId;
        _pendingDiffs.Clear();
        
        IsReadyForBoardDiffs = true;
        
        Debug.Log("Finalizing client bootstrap.");
        if (_clientReady || !_inputHandler || _actions == null || _board == null)
            return;

        Debug.Log("Initializing input handler.");
        _inputHandler.Initialize(_mapper, _actions);
        _inputHandler.RequestSubmitted += OnRequestSubmitted;
        _inputHandler.HoverChanged += OnHoverChanged;
        _inputWired = true;

        _renderer?.Initialise(_board, _mapper, _localPlayerId);

        _clientReady = true;
        
        _actions.SetTurnState(_bufferedIsMyTurn, _bufferedRemainingBudget);
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

        if (diffs != null)
        {
            var safe = Mathf.Clamp(count, 0, diffs.Length);
            for (var i = 0; i < safe; i++)
                _pendingDiffs.Add(diffs[i]);
        }
        
        if (!isFinalChunk)
            return;

        if (!_initialBoardApplied)
        {
            _initialBoardApplied = true;
            _board.Apply(_pendingDiffs);
            _pendingDiffs.Clear();
            
            if (_isLoading)
            {
                _isLoading = false;
                EventBus.Raise(new HideLoadingScreenEvent());
                Debug.Log("[ClientManager] Initial board applied, hiding loading screen.");
            }
            return;
        }

        _blastStamper.Stamp(_pendingDiffs);
        _audio.Interpret(_pendingDiffs);
        _board.Apply(_pendingDiffs); // raises Changed once
        _pendingDiffs.Clear();
        
        
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

        RaiseLocalTurnState();
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

        _playerActionsById[currentPlayingPlayer.PlayerId] = currentPlayingPlayer;
        if (currentPlayingPlayer.PlayerId == _localPlayerId)
        {
            _bufferedIsMyTurn = true;
            _bufferedRemainingBudget = currentPlayingPlayer.CurrentActionAmount;
        }
        else
        {
            _bufferedIsMyTurn = false;
        }
        _actions.SetTurnState(_bufferedIsMyTurn, _bufferedRemainingBudget);
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_CurrentPlayingPlayerChanged for {name} playerId={currentPlayingPlayer.PlayerId} current budget={_bufferedRemainingBudget}, is my turn={_bufferedIsMyTurn}.");

        foreach (var kvp in NetworkManager.Instance.GetPlayerDataMap().Where(kvp => currentPlayingPlayer.PlayerId == kvp.Key.PlayerId))
        {
            OnPlayerTurnChanged.Invoke(currentPlayingPlayer.PlayerId == _localPlayerId
                ? "Your"
                : $"{kvp.Value.DisplayName.ToString()}'s");
        }
        
        RaiseLocalTurnState();
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
        
        _playerActionsById[upcomingPlayer.PlayerId] = upcomingPlayer;
        _bufferedIsMyTurn = upcomingPlayer.PlayerId == _localPlayerId;
        _bufferedRemainingBudget = upcomingPlayer.CurrentActionAmount;
        _actions.SetTurnState(_bufferedIsMyTurn, _bufferedRemainingBudget);
        
        foreach (var kvp in NetworkManager.Instance.GetPlayerDataMap().Where(kvp => upcomingPlayer.PlayerId == kvp.Key.PlayerId))
        {
            OnPlayerTurnChanged.Invoke(upcomingPlayer.PlayerId == _localPlayerId
                ? "Your"
                : $"{kvp.Value.DisplayName.ToString()}'s");
        }
        
        RaiseLocalTurnState();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_EndGame(PlayerRef winner)
    {
        GameTraceLogger.Rpc(TraceLogsEnabled, $"RPC_EndGame for {name} winner={winner}.");

        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_EndGame on a non-input-authority peer.");
            return;
        }
        
        NetworkManager.Instance.InGameUIInstance?.OnMatchEnded?.Invoke(winner);
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

    private void RaiseLocalTurnState()
    {
        var current = 0;
        var max = 0;
        if (_playerActionsById.TryGetValue(_localPlayerId, out var localData))
        {
            current = localData.CurrentActionAmount;
            max = localData.MaxActionAmountPerTurn;
        }

        EventBus.Raise(new LocalTurnStateChangedEvent
            {
                IsMyTurn = _bufferedIsMyTurn,
                CurrentBudget = current,
                MaxBudget = max
            }
        );
    }
}
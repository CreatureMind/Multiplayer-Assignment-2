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
    // Cells per diff RPC; large blasts are split into sequential chunks. 64 * 8B = 512B, safely under the reliable RPC limit.
    public const int MaxDiffsPerRpc = 64;
    
    [Networked] public PlayerRef Player { get; private set; }
    [Networked] public byte PlayerId { get; private set; } // 1-based; 0 = no owner
    
    private ServerGameManager _server; // server-side only
    private ClientBoardCache _board; // client-side only
    private BoardCoordinateMapper _mapper;
    private PlayerActionController _actions;
    private IBoardRenderer _renderer;
    private InputHandler _inputHandler;
    private bool _clientReady;
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
    public void InstantiateClientManager(ServerGameManager server, byte seatId)
    {
        if (!HasStateAuthority)
            return;
        
        _server = server ?? throw new ArgumentNullException(nameof(server));
        Player = Object.InputAuthority;
        PlayerId = seatId;
        name = $"ClientManager_P{seatId}";
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
        RPC_ClientReady();
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_inputHandler)
        {
            _inputHandler.RequestSubmitted -= OnRequestSubmitted;
            _inputHandler.HoverChanged -= OnHoverChanged;
        }
        if (_actions != null)
            _actions.HighlightsInvalidated -= OnHighlightsInvalidated;
        if (_board != null)
            _board.Changed -= OnBoardChanged;

        _clientReady = false;
        _server = null;
        _playerActionsById.Clear();
    }
    
    #region Client -> Server
    // Readiness handshake: decouples client init from server spawn ordering (no scene-context race).
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ClientReady()
    {
        if (!_server)
        {
            Debug.LogError("[ClientManager] RPC_ClientReady on a peer with no ServerGameManager.");
            return;
        }
        // Hands readiness back to server so it can initialise this client and stream the full board diff.
        _server.OnClientReady(this);
    }
    
    // Carries an INTENT, not a target type: move-into-empty and capture both yield Soldier, and BuildBase carries a window origin.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_RequestMove(Vector2Int cell, MoveIntent intent)
    {
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

        _inputHandler.Initialize(_mapper, _actions);
        _inputHandler.RequestSubmitted += OnRequestSubmitted;
        _inputHandler.HoverChanged += OnHoverChanged;

        _renderer?.Initialise(_board, _mapper, playerId);
        _clientReady = true;
    }

    // A chunk of this player's projected diff. Reliable + cumulative: a dropped chunk desyncs permanently.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ApplyDiffs(CellDiff[] diffs, int count, NetworkBool isFinalChunk)
    {
        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_ApplyDiffs on a non-input-authority peer.");
            return;
        }
        
        if (!_clientReady)
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
    }

    // Turn ownership + budget. Budget is MIRRORED, never computed locally (conquering a base grants +N mid-turn).
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_SetTurnState(NetworkBool isMyTurn, int remainingBudget)
    {
        if (!HasInputAuthority)
        {
            Debug.LogWarning("[ClientManager] RPC_SetTurnState on a non-input-authority peer.");
            return;
        }
        if (!_clientReady)
            return;
        _actions.SetTurnState(isMyTurn, remainingBudget);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_InitialisePlayerActions(PlayerActionData[] playerActions, int count)
    {
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
        => RPC_RequestMove(request.Cell, request.Intent);
    private void OnBoardChanged(IReadOnlyList<CellDiff> _)
        => _actions.OnBoardChanged(); // renderer subscribes to _board.Changed itself
    private void OnHighlightsInvalidated()
        => _renderer?.SetHighlights(_actions.CurrentHighlights);
    private void OnHoverChanged(Vector2Int? cell)
        => _renderer?.SetHover(cell);
}
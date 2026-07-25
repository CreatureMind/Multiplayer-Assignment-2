using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Implemented by BoardView.
// Declared as an interface so ClientManager can be completed and compiled before the renderer exists, and so the renderer can be subbed in tests.
public interface IBoardRenderer
{
    void Initialise(ClientBoardCache board, BoardCoordinateMapper mapper, byte localPlayerId);
    void SetHighlights(IReadOnlyCollection<Vector2Int> cells);
    void SetHover(Vector2Int? cell);
}

// One per player, spawned by the server with InputAuthority assigned to that player.
// Exists on every peer, but only the instance where Object.HasInputAuthority is true builds a client stack.
// This is the client's COMPOSITION ROOT: it owns board cache, coordinate mapper, action modes, and controller, and wires InputHandler to the server.
// It is a mediator - it contains no gameplay rules, only routing.
// Server -> client traffic is per-viewer projected diffs over RPC, never [Networked] state.
// That is not a style choice: replicated board state would hand every client the true type of every bomb.
public class ClientManager : NetworkBehaviour
{
    // Cells per diff RPC. Bomb chains can clear far more than this, so the server splits large batches into sequential chunks.
    // VERIFY against the RPC payload limit before shipping.
    public const int MaxDiffsPerRpc = 64;
    
    [Networked] public PlayerRef Player { get; private set; }
    // 1-based, matches TileState.OwnerId. 0 is reserved for "no owner"
    [Networked] public byte PlayerId { get; private set; }
    
    // Server-side only
    // INSTANCE field. See notes: this was static and that was a bug. (static field would be shared among ALL clients)
    private ServerGameManager _server;
    
    // Client-side only (built on the local player's instance)
    private ClientBoardCache _board;
    private BoardCoordinateMapper _mapper;
    private PlayerActionController _actions;
    private IBoardRenderer _renderer;
    private InputHandler _inputHandler;
    private bool _clientReady;
    
    // Chunk buffer.
    // Diffs accumulate here until the server flags the final chunk, so a 200-cell blast produces ONE cache update and on legal-move recompute rather than four.
    private readonly List<CellDiff> _pendingDiffs = new List<CellDiff>(MaxDiffsPerRpc);

    public void InitialiseServer(ServerGameManager server, byte playerId)
    {
        if (!HasStateAuthority)
            return;

        _server = server ?? throw new ArgumentNullException(nameof(server));
        Player = Object.InputAuthority;
        PlayerId = playerId;
        name = $"ClientManager_P{playerId}";
    }
    
    // Server-side setup. Called by ServerGameManager immediately after spawn.
    public void InstantiateClientManager(ServerGameManager server, byte playerId)
    {
        if (!HasStateAuthority)
            return;
        
        _server = server ?? throw new ArgumentNullException(nameof(server));
        Player = Object.InputAuthority;
        PlayerId = playerId;
        name = $"ClientManager_P{playerId}";
    }
    
    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return; // someone else's ClientManager

        var context = ClientSceneContext.Instance;
        if (!context)
        {
            Debug.LogError("[ClientManager] No ClientSceneContext in the scene; cannot initialise local client.");
            return;
        }

        _renderer = context.Renderer;
        _inputHandler = context.InputHandler;

        // Board dimensions arrive in RPC_InitialiseClient, so the rest of the stack is built there.
        // We only cache the rig references here.
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
            _board.Changed  -= OnBoardChanged;

        _clientReady = false;
        _server = null;
    }
    
    #region Server -> This Client
    // One-shot client bootstrap. Sent once, after the board is authored.
    // The GDD rules out reconnect and late-join, so a single init plus a diff stream is sufficient
    // No [Networked] board state is needed at all.
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
    
    // A chunk of this player's projected diff. Already filtered by TileProjector server-side, so enemy bombs arrive as Soldier
    // The true type is never on this peer in any form.
    // Reliable channel: diffs are cumulative, so a dropped chunk desyncs the board permanently.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_ApplyDiffs(CellDiff[] diffs, int count, NetworkBool isFinalChunk)
    {
        if (!_clientReady)
        {
            Debug.LogWarning("[ClientManager] Diff received before initialisation; dropped.");
            return;
        }

        if (diffs != null)
        {
            var safeCount = Mathf.Clamp(count, 0, diffs.Length);
            for (var i = 0; i < safeCount; i++)
                _pendingDiffs.Add(diffs[i]);
        }

        if (!isFinalChunk)
            return; // more chunks coming; hold the update

        _board.Apply(_pendingDiffs); // raises Changed once
        _pendingDiffs.Clear();
    }
    
    // Turn ownership and remaining budget.
    // Budget is MIRRORED, never computed locally: conquering a base mid-turn grants +3 immediately, and only the server knows that happened.
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_SetTurnState(NetworkBool isMyTurn, int remainingBudget)
    {
        if (!_clientReady)
            return;
        _actions.SetTurnState(isMyTurn, remainingBudget);
    }
    #endregion
    
    #region This Client -> Server
    // Carries an INTENT, not a target TileType.
    // A type cannot express this vocabulary: moving into an empty cell and capturing an enemy soldier both produce Soldier,
    // and BuildBase needs to carry a 4x4 window origin rather than a target cell.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_RequestMove(Vector2Int cell, MoveIntent intent)
    {
        if (!_server)
        {
            Debug.LogError("[ClientManager] RPC_RequestMove reached a peer with no ServerGameManager.");
            return;
        }
        
        // The client already filtered illegal clicks, but that is a UX affordance and nothing more.
        // This is the authoritative path.
        // TODO: implement in ServerGameManager this: _server.HandleMoveRequest(PlayerId, cell, intent);
        // TODO: BoardManager.cs can't stay as written, "[Networked, Capacity(1024)] NetworkArray<TileState> Tiles" must be deleted.
        // TODO: Fusion replicates networked state to all clients, so every client would receive the true TileType of every bomb, making them not truly hidden.
        // TODO: The mechanic is dead the moment that array syncs. BoardData becomes a plain server-side TileState[,], and client get projected diff only.
        // TODO: MaxBoardTiles = 1024 is below specs, as board can be 50x50, which requires 2500 cells, so ValidateBoardDimensions throws on a legal map.
        // TODO: If you make the board as plain C#, the cap can go.
        
    }
    #endregion

    private void OnRequestSubmitted(MoveRequest request)
        => RPC_RequestMove(request.Cell, request.Intent);
    
    private void OnBoardChanged(IReadOnlyCollection<CellDiff> _)
        => _actions.OnBoardChanged(); // renderer subscribes to _board.Changed itself
    
    private void OnHighlightsInvalidated()
        => _renderer?.SetHighlights(_actions.CurrentHighlights);
    
    private void OnHoverChanged(Vector2Int? cell)
        => _renderer?.SetHover(cell);
}
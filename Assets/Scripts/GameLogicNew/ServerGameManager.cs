using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Fusion;
using UnityEngine;

public sealed class ServerGameManager : NetworkBehaviour
{
    public static ServerGameManager Instance { get; private set; }

    [SerializeField] private GameDataSO data;
    [SerializeField] private bool _traceLogsEnabledAtStart = true;
    private BoardManager _boardManagerInstance;
    private BoardDiffBroadcaster _boardDiffBroadcaster;
    private TurnManager _turnManagerInstance;
    private TurnDiffBroadcaster _turnDiffBroadcaster;
    private List<ClientManager> _clientManagers = new();

    private bool _boardManagerSpawned;
    private bool _TurnManagerSpawned;
    private bool _ClientManagerSpawned;
    private NetworkBool _initRequested;
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
    private int _currentPlayerCount;
    private readonly HashSet<byte> _readyClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _initialisedClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _diffHandshakeClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _loggedSkippedLiveDiffClientIds = new HashSet<byte>();

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if ((bool)TraceLogsEnabled != _traceLogsEnabledAtStart)
            TraceLogsEnabled = _traceLogsEnabledAtStart;

        SyncTraceLoggingToManagers();
    }

    public void RequestInstantiation()
    {
        Debug.Log("Requesting instantiation of game managers...");

        if (_initRequested) return;
        
        RPC_RequestInstantiationFromScene();
        
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private async void RPC_RequestInstantiationFromScene()
    {
        if (_initRequested) return;
        
        Debug.Log("RPC received: Requesting instantiation of game managers...");
        
        if (!HasStateAuthority) return;
        _initRequested = true;
        TraceLogsEnabled = _traceLogsEnabledAtStart;
        _readyClientIds.Clear();
        _initialisedClientIds.Clear();
        _diffHandshakeClientIds.Clear();
        _loggedSkippedLiveDiffClientIds.Clear();
        
        await InstantiateClientManagers();
        await SpawnBoardManager();
    }

    private static async Task<T> SpawnAndWaitAsync<T>(
        NetworkRunner runner,
        T prefab,
        Vector3 position,
        Quaternion rotation,
        PlayerRef? inputAuthority = null,
        float timeoutSeconds = 3f)
        where T : NetworkBehaviour
    {
        var tcs = new TaskCompletionSource<T>();

        runner.Spawn(
            prefab,
            position,
            rotation,
            inputAuthority,
            onBeforeSpawned: (_, obj) => tcs.TrySetResult(obj.GetComponent<T>()));

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var finished = await Task.WhenAny(tcs.Task, timeoutTask);

        return finished == tcs.Task ? tcs.Task.Result : null;
    }

    private async Task SpawnTurnManager()
    {
        Debug.Log("Attempting to spawn turn manager...");
        
        if (!HasStateAuthority) return;

        if (!data.TurnManagerPrefab)
        {
            Debug.LogWarning("TurnManager prefab is not assigned.");
            return;
        }
        
        if (!_ClientManagerSpawned)
        {
            Debug.LogWarning("ClientManagers have not been spawned yet. Cannot spawn TurnManager.");
            return;
        }
        
        if  (!data.TurnStats)
        {
            Debug.LogWarning("TurnStatsSO is not assigned in GameDataSO. Cannot spawn TurnManager.");
            return;
        }

        _turnManagerInstance = await SpawnAndWaitAsync(
            Runner,
            data.TurnManagerPrefab,
            Vector3.zero,
            Quaternion.identity);

        if (!_turnManagerInstance)
        {
            Debug.LogWarning("TurnManager spawn timed out or failed.");
            return;
        }

        _turnDiffBroadcaster = new TurnDiffBroadcaster(_turnManagerInstance, _clientManagers);
        _turnManagerInstance.SetTraceLoggingEnabled(TraceLogsEnabled);
        _turnManagerInstance.InstantiateTurnManager(_clientManagers, data.TurnStats, _turnDiffBroadcaster);
        _TurnManagerSpawned = true;
        
        Debug.Log("Successfully spawned turn manager.");
    }

    private async Task InstantiateClientManagers()
    {
        Debug.Log("Attempting to spawn client managers...");
        
        if (!HasStateAuthority) return;

        var players = Runner.ActivePlayers;

        //count all players for future reference and enforcement of minimum players 
        foreach (var player in players)
        {
            if(player.PlayerId == -1) continue;
            _currentPlayerCount++;
        }
        
        Debug.Log($"Received {_currentPlayerCount} players...");

        //enforcement of minimum players 
        if (!data.ValidatePlayerCount(_currentPlayerCount))
        {
            throw new InvalidOperationException(
                $"Invalid player count: {_currentPlayerCount}. Expected counts: {string.Join(", ", data.NumberOfPlayersToEnforce)}");
        }

        // create client manager per player
        foreach (var player in players)
        {
            if(player.PlayerId == -1) continue;
            var clientManager = await SpawnAndWaitAsync(
                Runner,
                data.ClientManagerPrefab,
                Vector3.zero,
                Quaternion.identity,
                player);

            if (!clientManager)
            {
                Debug.LogWarning($"ClientManager spawn timed out or failed for {player.PlayerId}.");
                continue;
            }

            clientManager.InstantiateClientManager(this, (byte)player.PlayerId);
            clientManager.SetTraceLoggingEnabled(TraceLogsEnabled);
            _clientManagers.Add(clientManager);

            Debug.Log($"Spawned Client Manager for {player.PlayerId}...");
        }

        if (_clientManagers.Count != _currentPlayerCount) return;
        
        _ClientManagerSpawned = true;
        
        Debug.Log("Successfully spawned all client managers.");
        
        await SpawnTurnManager();
    }


    private async Task SpawnBoardManager()
    {
        Debug.Log("Attempting to spawn board manager...");
        
        if (!HasStateAuthority) return;
        
        if (_boardManagerSpawned)
            return;

        if (!data.BoardManagerPrefab)
        {
            Debug.LogWarning("BoardManager prefab is not assigned.");
            return;
        }

        if (!HasStateAuthority)
            return;

        _boardManagerInstance = await SpawnAndWaitAsync(
            Runner,
            data.BoardManagerPrefab,
            Vector3.zero,
            Quaternion.identity);

        if (!_boardManagerInstance)
        {
            Debug.LogWarning("BoardManager spawn timed out or failed.");
            return;
        }

        _boardManagerSpawned = true;
        _boardManagerInstance.SetTraceLoggingEnabled(TraceLogsEnabled);
        
        
        _boardManagerInstance.InitializeBoardWithMadeMap_ServerOnly(data.StartingPosition);
        
        Debug.Log("Instantiated board manager...");

        _boardDiffBroadcaster = new BoardDiffBroadcaster(_boardManagerInstance, _clientManagers, CanReceiveLiveDiffs, OnLiveDiffSkipped);
        
        Debug.Log("Spawned board diff broadcaster...");
        
        TryInitialiseReadyClients();
        
        while (!_turnManagerInstance || !_TurnManagerSpawned)
        {
            await Task.Yield();
        }
        
        var changedBases= _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        var setupDiffCells = new List<Vector2Int>();
        foreach (var baseBottomLeft in changedBases)
            AddBaseCells(baseBottomLeft, setupDiffCells);
        
        Debug.Log($"Added {changedBases.Count} bases...");
        
        _boardDiffBroadcaster?.Broadcast(setupDiffCells);

        int successCounter = 0;
        
        foreach (var cb in changedBases)
        {
            var result = _turnManagerInstance.PlayerBuiltBase(_boardManagerInstance.GetTileOwnerByIndex(cb));
            if (result == ActionResult.Success)
            {
                successCounter++;
                Debug.Log("Successfully built base: " + cb);
            }
        }
        
        if (successCounter == _currentPlayerCount)
        {
            Debug.Log("All players have successfully instantiated first bases.");
        }

        Debug.Log("Successfully spawned board manager.");
    }

    [ContextMenu("Request Move Manually")]
    public void HandleMoveRequest(ClientManager clientManager, Vector2Int cell, MoveIntent intent)
    {
        GameTraceLogger.Move(TraceLogsEnabled, $"HandleMoveRequest start player={clientManager?.PlayerId.ToString() ?? "null"}, intent={intent}, cell={cell}.");
        
        if (!HasStateAuthority)
        {
            GameTraceLogger.Move(TraceLogsEnabled, "HandleMoveRequest aborted: no state authority.");
            return;
        }

        if (HandleMoveRequestCheckAndUpdate(clientManager, cell, intent))
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"HandleMoveRequest success player={clientManager.PlayerId}, intent={intent}, cell={cell}.");
        }
        else
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"HandleMoveRequest rejected player={clientManager?.PlayerId.ToString() ?? "null"}, intent={intent}, cell={cell}.");
        }
    }

    private bool HandleMoveRequestCheckAndUpdate(ClientManager clientManager, Vector2Int cell, MoveIntent intent)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Move(TraceLogsEnabled, "Rejected move: no state authority.");
            return false;
        }

        if (!clientManager)
        {
            GameTraceLogger.Move(TraceLogsEnabled, "Rejected move: client manager was null.");
            return false;
        }

        if (!_boardManagerInstance)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Rejected move P{clientManager.PlayerId}: board manager missing.");
            return false;
        }

        if (!_turnManagerInstance)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Rejected move P{clientManager.PlayerId}: turn manager missing.");
            return false;
        }

        // check turnManager first cause it's cheaper that the DFS checks in boardManager
        var isValidTurn = _turnManagerInstance.ValidatePlayerTurn(clientManager.PlayerId);
        GameTraceLogger.Move(TraceLogsEnabled, $"Turn validation P{clientManager.PlayerId}: {isValidTurn}.");
        if (!isValidTurn)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Rejected move P{clientManager.PlayerId}: not player's turn.");
            return false;
        }

        if (intent == MoveIntent.Pass) // pass intent is always valid, no need to check board state
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Pass intent accepted for P{clientManager.PlayerId}; ending turn.");
            HandlePassIntent(clientManager);
            return true;
        }

        var hasResourcesForIntent = _turnManagerInstance.ValidatePlayerIntent(clientManager.PlayerId, intent);
        GameTraceLogger.Move(TraceLogsEnabled,
            $"Intent validation P{clientManager.PlayerId}, intent={intent}: {hasResourcesForIntent}.");
        if (!hasResourcesForIntent)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Rejected move P{clientManager.PlayerId}: insufficient resources for intent {intent}.");
            return false;
        }

        var actionResult = ActionResult.Success;

        var boardValidationPassed = _boardManagerInstance.ValidateBoardChange(cell, clientManager.PlayerId, intent);
        GameTraceLogger.Move(TraceLogsEnabled,
            $"Board validation P{clientManager.PlayerId}, intent={intent}, cell={cell}: {boardValidationPassed}.");
        if (boardValidationPassed == ValidationType.False)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Board validation failed for P{clientManager.PlayerId}, intent={intent}, cell={cell}.");
            return false;
        }
        
        if (boardValidationPassed == ValidationType.Bomb)
        {
            var explosionChangedCells = CascadingExplosionLogic(cell);
            if (explosionChangedCells.Count > 0)
            {
                GameTraceLogger.Move(TraceLogsEnabled, $"Broadcasting {explosionChangedCells.Count} explosion cells for P{clientManager.PlayerId}.");
                _boardDiffBroadcaster?.Broadcast(explosionChangedCells);
            }

            switch (intent)
            {
                case MoveIntent.MoveSoldier:
                    actionResult = _turnManagerInstance.PlayerPlacedPawn(clientManager.PlayerId);
                    break;
                case MoveIntent.PlaceBomb:
                    actionResult = _turnManagerInstance.PlayerPlacedBomb(clientManager.PlayerId);
                    break;
                default:
                    GameTraceLogger.Move(TraceLogsEnabled, $"Bomb-triggered path received unsupported intent={intent} for P{clientManager.PlayerId}.");
                    return false;
            }

            GameTraceLogger.Move(TraceLogsEnabled, $"Explosion action result for P{clientManager.PlayerId}, intent={intent}: {actionResult}.");
            if (actionResult == ActionResult.NotStateAuthority)
                return false;

            if (actionResult == ActionResult.SuccessAndTurnEnded)
            {
                GameTraceLogger.Move(TraceLogsEnabled, $"Ending turn for P{clientManager.PlayerId} due to explosion action budget depletion.");
                _turnManagerInstance.EndPlayerTurn(clientManager.PlayerId);
            }

            return true;
        }
        
        Vector2Int bottomLeftCorner =  cell;
        if (intent == MoveIntent.BuildBase)
        {
            if (!BoardUtilities.TryGetBottomLeftCornerOfBase4By4(cell, clientManager.PlayerId, out bottomLeftCorner))
            {
                GameTraceLogger.Move(TraceLogsEnabled,
                    $"BuildBase ring validation failed for P{clientManager.PlayerId} at {cell}.");
                return false;
            }
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Applying board mutation for P{clientManager.PlayerId}, intent={intent}, cell={cell}.");
        _boardManagerInstance.SetTileServerOnly(bottomLeftCorner, clientManager.PlayerId, intent);

        var changedCells = new List<Vector2Int>();
        if (intent == MoveIntent.BuildBase)
            AddBuildBaseCoreCells(cell, changedCells);
        else
            changedCells.Add(cell);

        switch (intent)
        {
            case MoveIntent.MoveSoldier:
                actionResult = _turnManagerInstance.PlayerPlacedPawn(clientManager.PlayerId);
                break;
            case MoveIntent.PlaceBomb:
                actionResult = _turnManagerInstance.PlayerPlacedBomb(clientManager.PlayerId);
                break;
            case MoveIntent.BuildBase:
                actionResult = _turnManagerInstance.PlayerBuiltBase(clientManager.PlayerId);
                break;
        }
        GameTraceLogger.Move(TraceLogsEnabled, $"Turn action result for P{clientManager.PlayerId}, intent={intent}: {actionResult}.");
        if (actionResult == ActionResult.NotStateAuthority)
            return false;

        var changedBases = _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        GameTraceLogger.Move(TraceLogsEnabled, $"Conquered base updates after move: {changedBases.Count}.");
        foreach (var baseBottomLeft in changedBases)
        {
            AddBaseCells(baseBottomLeft, changedCells);
            var ownerId = _boardManagerInstance.GetTileOwnerByIndex(baseBottomLeft);
            GameTraceLogger.Move(TraceLogsEnabled, $"Applying base gain for owner P{ownerId} at {baseBottomLeft}.");
            _turnManagerInstance.PlayerBuiltBase(ownerId);
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Check for base conquer P{clientManager.PlayerId} after intent={intent}.");
        ServerBoardRules.ConqueredBasesByPawnPlacementCheck(_boardManagerInstance, clientManager.PlayerId, cell, out var conqueredBases);
        if (conqueredBases.Count > 0)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Conquered bases by P{clientManager.PlayerId} after intent={intent}: {conqueredBases.Count}.");
            foreach (var baseBottomLeft in conqueredBases)
            {
                AddBaseCells(baseBottomLeft, changedCells);
                GameTraceLogger.Move(TraceLogsEnabled, $"Applying base gain for owner P{clientManager.PlayerId} at {baseBottomLeft}.");
                _turnManagerInstance.PlayerBuiltBase(clientManager.PlayerId);
            }
        }   

        GameTraceLogger.Move(TraceLogsEnabled, $"Broadcasting {changedCells.Count} changed cells.");
        _boardDiffBroadcaster?.Broadcast(changedCells);

        if (intent == MoveIntent.BuildBase || actionResult == ActionResult.SuccessAndTurnEnded)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Ending turn for P{clientManager.PlayerId}. reason={(intent == MoveIntent.BuildBase ? "BuildBase intent" : "ActionResult.SuccessAndTurnEnded")}.");
            _turnManagerInstance.EndPlayerTurn(clientManager.PlayerId);
        }
        else
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Turn remains with P{clientManager.PlayerId} after intent={intent}.");
        }
        
        GameTraceLogger.Move(TraceLogsEnabled, $"Check for motherload conquer P{clientManager.PlayerId} after intent={intent}.");
        if (ServerBoardRules.MotherloadConqueredWinConditionCheck(_boardManagerInstance, clientManager.PlayerId, cell))
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Motherload conquered by P{clientManager.PlayerId} after intent={intent}. Ending game.");
            _turnManagerInstance.EndGame(clientManager.PlayerId);
        }

        return true;
    }

    private List<Vector2Int> CascadingExplosionLogic(Vector2Int cell)
    {
        var toExplode = BoardUtilities.DetonateBomb(cell);
        var changedCells = new List<Vector2Int>(toExplode.Count);

        while (toExplode.Count > 0)
        {
            var explodeCell = toExplode.Dequeue();
            if (_boardManagerInstance.SetTileEmptyServerOnly(explodeCell))
                changedCells.Add(explodeCell);
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"CascadingExplosionLogic cleared {changedCells.Count} cells from epicenter {cell}.");
        return changedCells;
    }

    private void HandlePassIntent(ClientManager clientManager)
    {
        if (!HasStateAuthority || !_turnManagerInstance) return;
        GameTraceLogger.Move(TraceLogsEnabled, $"HandlePassIntent ending turn for P{clientManager.PlayerId}.");
        _turnManagerInstance.EndPlayerTurn(clientManager.PlayerId);
    }

    public void OnClientReady(ClientManager clientManager)
    {
        if (!HasStateAuthority || !clientManager)
            return;

        _readyClientIds.Add(clientManager.PlayerId);
        GameTraceLogger.Handshake(TraceLogsEnabled, $"Server accepted readiness for P{clientManager.PlayerId}.");
        TryInitialiseReadyClient(clientManager);
    }

    public void OnClientInitFinished(ClientManager clientManager)
    {
        if (!HasStateAuthority || !clientManager)
            return;

        if (!_initialisedClientIds.Contains(clientManager.PlayerId))
        {
            Debug.LogWarning($"[ServerGameManager] Ignoring RPC_ClientInitFinished from P{clientManager.PlayerId} before init was sent.");
            return;
        }

        if (_diffHandshakeClientIds.Add(clientManager.PlayerId))
        {
            _loggedSkippedLiveDiffClientIds.Remove(clientManager.PlayerId);
            GameTraceLogger.Handshake(TraceLogsEnabled, $"Server marked P{clientManager.PlayerId} as live-diff enabled.");
        }
    }

    private void TryInitialiseReadyClients()
    {
        foreach (var clientManager in _clientManagers)
            TryInitialiseReadyClient(clientManager);
    }

    private void TryInitialiseReadyClient(ClientManager clientManager)
    {
        if (!clientManager || !_boardManagerSpawned || !_boardManagerInstance || _boardDiffBroadcaster == null)
            return;

        if (!_readyClientIds.Contains(clientManager.PlayerId) || _initialisedClientIds.Contains(clientManager.PlayerId))
            return;

        _diffHandshakeClientIds.Remove(clientManager.PlayerId);
        _loggedSkippedLiveDiffClientIds.Remove(clientManager.PlayerId);
        clientManager.RPC_InitialiseClient(clientManager.PlayerId, (short)_boardManagerInstance.Width, (short)_boardManagerInstance.Height);
        _boardDiffBroadcaster.SendFullBoard(clientManager);
        _turnManagerInstance?.SyncClientTurnState(clientManager);
        _initialisedClientIds.Add(clientManager.PlayerId);
    }

    private bool CanReceiveLiveDiffs(ClientManager clientManager)
    {
        return clientManager && _diffHandshakeClientIds.Contains(clientManager.PlayerId);
    }

    private void OnLiveDiffSkipped(ClientManager clientManager)
    {
        if (!clientManager)
            return;

        var playerId = clientManager.PlayerId;
        if (_loggedSkippedLiveDiffClientIds.Add(playerId))
            GameTraceLogger.Handshake(TraceLogsEnabled, $"Skipping live diff broadcast for P{playerId} until RPC_ClientInitFinished arrives.");
    }

    private void SyncTraceLoggingToManagers()
    {
        if (_boardManagerInstance && _boardManagerInstance.TraceLogsEnabled != TraceLogsEnabled)
            _boardManagerInstance.SetTraceLoggingEnabled(TraceLogsEnabled);

        if (_turnManagerInstance && _turnManagerInstance.TraceLogsEnabled != TraceLogsEnabled)
            _turnManagerInstance.SetTraceLoggingEnabled(TraceLogsEnabled);

        foreach (var clientManager in _clientManagers)
        {
            if (clientManager && clientManager.TraceLogsEnabled != TraceLogsEnabled)
                clientManager.SetTraceLoggingEnabled(TraceLogsEnabled);
        }
    }

    private void AddBaseCells(Vector2Int bottomLeft, List<Vector2Int> changedCells)
    {
        GameTraceLogger.Move(TraceLogsEnabled, $"Adding base cells from bottom-left {bottomLeft}.");
        
        changedCells.Add(bottomLeft);
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y));
        changedCells.Add(new Vector2Int(bottomLeft.x, bottomLeft.y + 1));
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1));

        GameTraceLogger.Move(TraceLogsEnabled, "Added base cells.");
    }

    private void AddBuildBaseCoreCells(Vector2Int buildWindowOrigin, List<Vector2Int> changedCells)
    {
        GameTraceLogger.Move(TraceLogsEnabled, $"Adding build-base core cells from origin {buildWindowOrigin}.");
        
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 2));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 2));
        
        GameTraceLogger.Move(TraceLogsEnabled, "Added motherload cells.");
    }
}
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

    public override void Spawned()
    {
        if (Instance && Instance != this)
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
        _turnManagerInstance.InstantiateTurnManager(this, _clientManagers, data.TurnStats, _turnDiffBroadcaster);
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

        if (_clientManagers.Count != _currentPlayerCount) throw new InvalidOperationException(
            $"Mismatch in spawned client managers ({_clientManagers.Count}) and expected player count ({_currentPlayerCount}).");
        
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
        
        while (!_turnManagerInstance || !_TurnManagerSpawned)
        {
            await Task.Yield();
        }

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

        var keyList = _turnManagerInstance.GetKeyList();
        
        _boardManagerInstance.InitializeBoardWithMadeMap_ServerOnly(data.StartingPosition, keyList);
        
        Debug.Log("Instantiated board manager...");

        _boardDiffBroadcaster = new BoardDiffBroadcaster(_boardManagerInstance, _clientManagers);
        
        Debug.Log("Spawned board diff broadcaster...");
        
        await TrySendFirstBoardUpdatesToAllClients();
        
        var changedBases= _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        var setupDiffCells = new List<Vector2Int>();
        foreach (var baseBottomLeft in changedBases)
            AddBaseCells(baseBottomLeft, setupDiffCells);
        
        Debug.Log($"Added {changedBases.Count} bases...");
        
        _boardDiffBroadcaster?.Broadcast(setupDiffCells);

        int successCounter = 0;
        
        foreach (var changedBasePos in changedBases)
        {
            var result = _turnManagerInstance.PlayerBuiltBase(_boardManagerInstance.GetTileOwnerByIndex(changedBasePos));
            if (result == ActionResult.Success)
            {
                successCounter++;
                Debug.Log("Successfully built base: " + changedBasePos);
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
        if (!TryValidateMoveRequestInstances(clientManager, cell, intent, out var request))
            return false;

        var turnValidationResult = ValidateTurnAndIntentResources(request);
        if (turnValidationResult == MovePipelineGateResult.Rejected)
            return false;
        if (turnValidationResult == MovePipelineGateResult.Completed)
            return true;

        if (!TryValidateBoardChange(ref request))
            return false;

        var changeSet = CreateMoveChangeSet(request);
        if (!ApplyBoardChanges(request, changeSet))
            return false;

        BroadcastBoardChanges(request, changeSet);

        if (!ApplyTurnAndGameChanges(request, changeSet))
            return false;

        BroadcastTurnChanges(request, changeSet);
        return true;
    }

    private bool TryValidateMoveRequestInstances(ClientManager clientManager, Vector2Int cell, MoveIntent intent, out MoveRequestContext request)
    {
        request = default;

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

        request = new MoveRequestContext(clientManager, cell, intent);
        return true;
    }

    private MovePipelineGateResult ValidateTurnAndIntentResources(in MoveRequestContext request)
    {
        // check turnManager first cause it's cheaper that the DFS checks in boardManager
        var isValidTurn = _turnManagerInstance.ValidatePlayerTurn(request.PlayerId);
        GameTraceLogger.Move(TraceLogsEnabled, $"Turn validation P{request.PlayerId}: {isValidTurn}.");
        if (!isValidTurn)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Rejected move P{request.PlayerId}: not player's turn.");
            return MovePipelineGateResult.Rejected;
        }

        if (request.Intent == MoveIntent.Pass) // pass intent is always valid, no need to check board state
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Pass intent accepted for P{request.PlayerId}; ending turn.");
            HandlePassIntent(request.ClientManager);
            return MovePipelineGateResult.Completed;
        }

        var hasResourcesForIntent = _turnManagerInstance.ValidatePlayerIntent(request.PlayerId, request.Intent);
        GameTraceLogger.Move(TraceLogsEnabled,
            $"Intent validation P{request.PlayerId}, intent={request.Intent}: {hasResourcesForIntent}.");
        if (!hasResourcesForIntent)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Rejected move P{request.PlayerId}: insufficient resources for intent {request.Intent}.");
            return MovePipelineGateResult.Rejected;
        }

        return MovePipelineGateResult.Continue;
    }

    private bool TryValidateBoardChange(ref MoveRequestContext request)
    {
        request.BoardValidation = _boardManagerInstance.ValidateBoardChange(request.RequestedCell, request.PlayerId, request.Intent);
        GameTraceLogger.Move(TraceLogsEnabled,
            $"Board validation P{request.PlayerId}, intent={request.Intent}, cell={request.RequestedCell}: {request.BoardValidation}.");
        if (request.BoardValidation == ValidationType.False)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Board validation failed for P{request.PlayerId}, intent={request.Intent}, cell={request.RequestedCell}.");
            return false;
        }

        var mutationCell = request.RequestedCell;
        if (request.Intent == MoveIntent.BuildBase)
        {
            if (!BoardUtilities.TryGetBottomLeftCornerOfBase4By4(request.RequestedCell, request.ClientManager.PlayerId, out mutationCell))
            {
                GameTraceLogger.Move(TraceLogsEnabled,
                    $"BuildBase ring validation failed for P{request.PlayerId} at {request.RequestedCell}.");
                return false;
            }
        }

        request.MutationCell = mutationCell;
        return true;
    }

    private MoveChangeSet CreateMoveChangeSet(in MoveRequestContext request)
    {
        var changeSet = new MoveChangeSet(request.Intent, request.RequestedCell, request.MutationCell, request.BoardValidation == ValidationType.Bomb);

        if (!changeSet.IsExplosionPath)
        {
            if (request.Intent == MoveIntent.BuildBase)
                AddBuildBaseCoreCells(request.RequestedCell, changeSet.BoardChangedCells);
            else
                changeSet.BoardChangedCells.Add(request.RequestedCell);
        }

        return changeSet;
    }

    private bool ApplyBoardChanges(in MoveRequestContext request, MoveChangeSet changeSet)
    {
        if (changeSet.IsExplosionPath)
        {
            var explosionChangedCells = CascadingExplosionLogic(request.RequestedCell);
            changeSet.BoardChangedCells.AddRange(explosionChangedCells);
            return true;
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Applying board mutation for P{request.PlayerId}, intent={request.Intent}, cell={request.RequestedCell}.");
        _boardManagerInstance.SetTileServerOnly(request.MutationCell, request.PlayerId, request.Intent);

        var changedBases = _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        GameTraceLogger.Move(TraceLogsEnabled, $"Conquered base updates after move: {changedBases.Count}.");
        foreach (var baseBottomLeft in changedBases)
        {
            AddBaseCells(baseBottomLeft, changeSet.BoardChangedCells);
            var ownerId = _boardManagerInstance.GetTileOwnerByIndex(baseBottomLeft);
            changeSet.BaseGainOwners.Add(ownerId);
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Check for base conquer P{request.PlayerId} after intent={request.Intent}.");
        ServerBoardRules.ConqueredBasesByPawnPlacementCheck(_boardManagerInstance, request.ClientManager.PlayerId, request.RequestedCell, out var conqueredBases);
        if (conqueredBases.Count > 0)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Conquered bases by P{request.PlayerId} after intent={request.Intent}: {conqueredBases.Count}.");
            foreach (var baseBottomLeft in conqueredBases)
            {
                AddBaseCells(baseBottomLeft, changeSet.BoardChangedCells);
                changeSet.BaseGainOwners.Add(request.PlayerId);
            }
        }

        return true;
    }

    private void BroadcastBoardChanges(in MoveRequestContext request, MoveChangeSet changeSet)
    {
        if (changeSet.IsExplosionPath)
        {
            if (changeSet.BoardChangedCells.Count > 0)
            {
                GameTraceLogger.Move(TraceLogsEnabled, $"Broadcasting {changeSet.BoardChangedCells.Count} explosion cells for P{request.PlayerId}.");
                _boardDiffBroadcaster?.Broadcast(changeSet.BoardChangedCells);
            }

            return;
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Broadcasting {changeSet.BoardChangedCells.Count} changed cells.");
        _boardDiffBroadcaster?.Broadcast(changeSet.BoardChangedCells);
    }

    private bool ApplyTurnAndGameChanges(in MoveRequestContext request, MoveChangeSet changeSet)
    {
        if (!TryApplyPrimaryTurnAction(request, out var actionResult))
            return false;

        changeSet.ActionResult = actionResult;
        if (actionResult == ActionResult.NotStateAuthority)
            return false;

        if (changeSet.IsExplosionPath)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Explosion action result for P{request.PlayerId}, intent={request.Intent}: {actionResult}.");
            if (actionResult == ActionResult.SuccessAndTurnEnded)
                changeSet.ShouldEndTurn = true;
        }
        else
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Turn action result for P{request.PlayerId}, intent={request.Intent}: {actionResult}.");
            foreach (var ownerId in changeSet.BaseGainOwners)
            {
                GameTraceLogger.Move(TraceLogsEnabled, $"Applying base gain for owner P{ownerId}.");
                _turnManagerInstance.PlayerBuiltBase(ownerId);
            }

            GameTraceLogger.Move(TraceLogsEnabled, $"Check for motherload conquer P{request.PlayerId} after intent={request.Intent}.");
            if (ServerBoardRules.MotherloadConqueredWinConditionCheck(_boardManagerInstance, request.ClientManager.PlayerId, request.RequestedCell))
            {
                GameTraceLogger.Move(TraceLogsEnabled, $"Motherload conquered by P{request.PlayerId} after intent={request.Intent}. Ending game.");
                changeSet.ShouldEndGame = true;
                EndGame((byte)request.PlayerId);
            }

            if (!changeSet.ShouldEndGame)
            {
                if (request.Intent == MoveIntent.BuildBase || actionResult == ActionResult.SuccessAndTurnEnded)
                    changeSet.ShouldEndTurn = true;
            }
        }

        if (changeSet.ShouldEndTurn)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Ending turn for P{request.PlayerId}.");
            _turnManagerInstance.EndPlayerTurn(request.PlayerId);
        }
        else
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Turn remains with P{request.PlayerId} after intent={request.Intent}.");
        }

        return true;
    }

    public void EndGame(byte requestPlayerId)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"EndGame called without state authority for P{requestPlayerId}.");
            return;
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Game ended for winner P{requestPlayerId}.");
    }

    private void BroadcastTurnChanges(in MoveRequestContext request, MoveChangeSet changeSet)
    {
        if (changeSet.ShouldEndGame)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Turn broadcast skipped because game ended for winner P{request.PlayerId}.");
            return;
        }

        if (changeSet.ShouldEndTurn)
        {
            GameTraceLogger.Move(TraceLogsEnabled, $"Turn changed broadcast emitted after ending turn for P{request.PlayerId}.");
            return;
        }

        GameTraceLogger.Move(TraceLogsEnabled, $"Turn-state updates already broadcast for active player P{request.PlayerId}.");
    }

    private bool TryApplyPrimaryTurnAction(in MoveRequestContext request, out ActionResult actionResult)
    {
        switch (request.Intent)
        {
            case MoveIntent.MoveSoldier:
                actionResult = _turnManagerInstance.PlayerPlacedPawn(request.PlayerId);
                return true;
            case MoveIntent.PlaceBomb:
                actionResult = _turnManagerInstance.PlayerPlacedBomb(request.PlayerId);
                return true;
            case MoveIntent.BuildBase:
                actionResult = _turnManagerInstance.PlayerBuiltBase(request.PlayerId);
                return true;
            default:
                actionResult = ActionResult.NotStateAuthority;
                GameTraceLogger.Move(TraceLogsEnabled, $"Unsupported intent={request.Intent} for P{request.PlayerId} during turn action stage.");
                return false;
        }
    }

    private enum MovePipelineGateResult
    {
        Rejected,
        Continue,
        Completed
    }

    private struct MoveRequestContext
    {
        public MoveRequestContext(ClientManager clientManager, Vector2Int requestedCell, MoveIntent intent)
        {
            ClientManager = clientManager;
            PlayerId = clientManager.PlayerId;
            RequestedCell = requestedCell;
            MutationCell = requestedCell;
            Intent = intent;
            BoardValidation = ValidationType.False;
        }

        public ClientManager ClientManager;
        public int PlayerId;
        public Vector2Int RequestedCell;
        public Vector2Int MutationCell;
        public MoveIntent Intent;
        public ValidationType BoardValidation;
    }

    private sealed class MoveChangeSet
    {
        public MoveChangeSet(MoveIntent intent, Vector2Int requestedCell, Vector2Int mutationCell, bool isExplosionPath)
        {
            Intent = intent;
            RequestedCell = requestedCell;
            MutationCell = mutationCell;
            IsExplosionPath = isExplosionPath;
        }

        public MoveIntent Intent { get; }
        public Vector2Int RequestedCell { get; }
        public Vector2Int MutationCell { get; }
        public bool IsExplosionPath { get; }
        public ActionResult ActionResult { get; set; }
        public bool ShouldEndTurn { get; set; }
        public bool ShouldEndGame { get; set; }
        public List<Vector2Int> BoardChangedCells { get; } = new List<Vector2Int>();
        public List<int> BaseGainOwners { get; } = new List<int>();
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

    

    private async Task TrySendFirstBoardUpdatesToAllClients()
    {
        foreach (var clientManager in _clientManagers)
            await TrySendFirstBoardUpdates(clientManager);
    }

    private async Task TrySendFirstBoardUpdates(ClientManager clientManager)
    {
        if (!clientManager || !_boardManagerSpawned || !_boardManagerInstance || _boardDiffBroadcaster == null)
            return;

        clientManager.RPC_InitialiseClient(clientManager.PlayerId, (short)_boardManagerInstance.Width, (short)_boardManagerInstance.Height);
        while (!clientManager.IsReadyForBoardDiffs)
        {
            // Yields back to the main thread until the next frame
            await Task.Yield();
        }

        _boardDiffBroadcaster.SendFullBoard(clientManager);
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
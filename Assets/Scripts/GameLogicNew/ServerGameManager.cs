using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Fusion;
using UnityEngine;

public sealed class ServerGameManager : NetworkBehaviour
{
    private const string HandshakeLogPrefix = "<color=#4DD0E1>[ClientHandshake]</color>";

    [SerializeField] private GameDataSO data;
    private BoardManager _boardManagerInstance;
    private BoardDiffBroadcaster _boardDiffBroadcaster;
    private TurnManager _turnManagerInstance;
    private TurnDiffBroadcaster _turnDiffBroadcaster;
    private List<ClientManager> _clientManagers = new();

    private bool _boardManagerSpawned;
    private bool _TurnManagerSpawned;
    private bool _ClientManagerSpawned;
    private NetworkBool _initRequested;
    private int _currentPlayerCount;
    private readonly HashSet<byte> _readyClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _initialisedClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _diffHandshakeClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _loggedSkippedLiveDiffClientIds = new HashSet<byte>();

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

    public void HandleMoveRequest(ClientManager clientManager, Vector2Int cell, MoveIntent intent)
    {
        Debug.Log("Attempting to execute move request...");
        
        if (!HasStateAuthority) return;

        if (HandleMoveRequestCheckAndUpdate(clientManager, cell, intent))
        {
            Debug.Log("Executed move request.");
        }
        else
        {
            Debug.Log("Failed to execute move request.");
        }
    }

    private bool HandleMoveRequestCheckAndUpdate(ClientManager clientManager, Vector2Int cell, MoveIntent intent)
    {
        if (!HasStateAuthority) return false;

        if (!_boardManagerInstance) return false;   
        
        if (!_turnManagerInstance) return false;

        // check turnManager first cause it's cheaper that the DFS checks in boardManager
        if (!_turnManagerInstance.ValidatePlayerTurn(clientManager.PlayerId)) return false;

        if (intent == MoveIntent.Pass)  // pass intent is always valid, no need to check board state
        {
            HandlePassIntent(clientManager);
            return true;
        }

        if (!_turnManagerInstance.ValidatePlayerIntent(clientManager.PlayerId, intent)) return false;

        if (!_boardManagerInstance.ValidateBoardChange(cell, clientManager.PlayerId, intent)) return false;
        
        _boardManagerInstance.SetTileServerOnly(cell, clientManager.PlayerId, intent);

        var changedCells = new List<Vector2Int>();
        if (intent == MoveIntent.BuildBase)
            AddBuildBaseCoreCells(cell, changedCells);
        else
            changedCells.Add(cell);
        var actionResult = ActionResult.Success;

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

        var changedBases = _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        foreach (var baseBottomLeft in changedBases)
        {
            AddBaseCells(baseBottomLeft, changedCells);
            var ownerId = _boardManagerInstance.GetTileOwnerByIndex(baseBottomLeft);
            _turnManagerInstance.PlayerBuiltBase(ownerId);
        }

        _boardDiffBroadcaster?.Broadcast(changedCells);

        if (intent == MoveIntent.BuildBase || actionResult == ActionResult.SuccessAndTurnEnded)
            _turnManagerInstance.EndPlayerTurn(clientManager.PlayerId);
        
        return true;
    }

    private void HandlePassIntent(ClientManager clientManager)
    {
        if (!HasStateAuthority || !_turnManagerInstance) return;
        _turnManagerInstance.EndPlayerTurn(clientManager.PlayerId);
    }

    public void OnClientReady(ClientManager clientManager)
    {
        if (!HasStateAuthority || !clientManager)
            return;

        _readyClientIds.Add(clientManager.PlayerId);
        Debug.Log($"{HandshakeLogPrefix} Server accepted readiness for P{clientManager.PlayerId}.");
        TryInitialiseReadyClient(clientManager);
    }

    public void OnClientInitFinished(ClientManager clientManager)
    {
        if (!HasStateAuthority || !clientManager)
            return;

        if (!_initialisedClientIds.Contains(clientManager.PlayerId))
        {
            Debug.LogWarning($"{HandshakeLogPrefix} Ignoring RPC_ClientInitFinished from P{clientManager.PlayerId} before init was sent.");
            return;
        }

        if (_diffHandshakeClientIds.Add(clientManager.PlayerId))
        {
            _loggedSkippedLiveDiffClientIds.Remove(clientManager.PlayerId);
            Debug.Log($"{HandshakeLogPrefix} Server marked P{clientManager.PlayerId} as live-diff enabled.");
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
            Debug.LogWarning($"{HandshakeLogPrefix} Skipping live diff broadcast for P{playerId} until RPC_ClientInitFinished arrives.");
    }

    private static void AddBaseCells(Vector2Int bottomLeft, List<Vector2Int> changedCells)
    {
        Debug.Log($"Attempting to add base cells for base at ({bottomLeft.x}, {bottomLeft.y})");
        
        changedCells.Add(bottomLeft);
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y));
        changedCells.Add(new Vector2Int(bottomLeft.x, bottomLeft.y + 1));
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1));

        Debug.Log("Added base cells.");
    }

    private static void AddBuildBaseCoreCells(Vector2Int buildWindowOrigin, List<Vector2Int> changedCells)
    {
        Debug.Log($"Attempting to add base cells for base at ({buildWindowOrigin.x}, {buildWindowOrigin.y})");
        
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 2));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 2));
        
        Debug.Log("Added motherload cells.");
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;

public sealed class ServerGameManager : NetworkBehaviour
{

    [SerializeField] private GameDataSO data;
    private BoardManager _boardManagerInstance;
    private BoardDiffBroadcaster _boardDiffBroadcaster;
    private TurnManager _turnManagerInstance;
    private TurnDiffBroadcaster _turnDiffBroadcaster;
    private List<ClientManager> _clientManagers = new();

    private bool _boardManagerSpawned;
    private bool _TurnManagerSpawned;
    private bool _ClientManagerSpawned;
    private int _currentPlayerCount;
    private readonly HashSet<byte> _readyClientIds = new HashSet<byte>();
    private readonly HashSet<byte> _initialisedClientIds = new HashSet<byte>();
    

    public override void Spawned()
    {
        if (!HasStateAuthority) return;

        base.Spawned();
        
        InstantiateClientManagers();
        SpawnBoardManager();
    }

    private void SpawnTurnManager()
    {
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

        _turnManagerInstance = Runner.Spawn(data.TurnManagerPrefab, Vector3.zero, Quaternion.identity);
        _turnDiffBroadcaster = new TurnDiffBroadcaster(_turnManagerInstance, _clientManagers);
        _turnManagerInstance.InstantiateTurnManager(_clientManagers, data.TurnStats, _turnDiffBroadcaster);
        _TurnManagerSpawned = true;
        
    }

    private void InstantiateClientManagers()
    {
        if (!HasStateAuthority) return;

        var players = Runner.ActivePlayers;

        //count all players for future reference and enforcement of minimum players 
        foreach (var player in players)
        {
            if(player.PlayerId == -1) continue;
            _currentPlayerCount++;
        }

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
            var clientManager = Runner.Spawn(data.ClientManagerPrefab, Vector3.zero, Quaternion.identity, player);
            clientManager.InstantiateClientManager(this, (byte)player.PlayerId);
            _clientManagers.Add(clientManager);
        }

        if (_clientManagers.Count != _currentPlayerCount) return;
        
        _ClientManagerSpawned = true;
        SpawnTurnManager();
    }


    private void SpawnBoardManager()
    {
        if (!HasStateAuthority) return;
        
        StartCoroutine(SpawnBoardManagerRoutine());
    }

    private IEnumerator SpawnBoardManagerRoutine()
    {
        if (_boardManagerSpawned)
            yield break;

        if (!data.BoardManagerPrefab)
        {
            Debug.LogWarning("BoardManager prefab is not assigned.");
            yield break;
        }

        if (!HasStateAuthority)
            yield break;

        _boardManagerInstance = Runner.Spawn(data.BoardManagerPrefab, Vector3.zero, Quaternion.identity);
        _boardManagerSpawned = true;
        _boardManagerInstance.InitializeBoardWithMadeMap_ServerOnly(data.StartingPosition);
        _boardDiffBroadcaster = new BoardDiffBroadcaster(_boardManagerInstance, _clientManagers);
        TryInitialiseReadyClients();
        
        
        yield return new WaitUntil(() => _turnManagerInstance && _TurnManagerSpawned);
        
        var changedBases= _boardManagerInstance.CheckForConqueredBasesAndUpdateBoardState();
        var setupDiffCells = new List<Vector2Int>();
        foreach (var baseBottomLeft in changedBases)
            AddBaseCells(baseBottomLeft, setupDiffCells);
        _boardDiffBroadcaster?.Broadcast(setupDiffCells);

        int successCounter = 0;
        
        foreach (var cb in changedBases)
        {
            var result = _turnManagerInstance.PlayerBuiltBase(_boardManagerInstance.GetTileOwnerByIndex(cb));
            if (result == ActionResult.Success)
            {
                successCounter++;
            }
        }
        
        if (successCounter == _currentPlayerCount)
        {
            Debug.Log("All players have successfully instantiated first bases.");
        }
    }

    public void HandleMoveRequest(ClientManager clientManager, Vector2Int cell, MoveIntent intent)
    {
        if (!HasStateAuthority) return;

        if (HandleMoveRequestCheckAndUpdate(clientManager, cell, intent))
        {
            // success, everything had already been updated in HandleMoveRequestCheckAndUpdate
        }
        else
        {
            //failed to do action
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
        TryInitialiseReadyClient(clientManager);
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

        clientManager.RPC_InitialiseClient(clientManager.PlayerId, (short)_boardManagerInstance.Width, (short)_boardManagerInstance.Height);
        _boardDiffBroadcaster.SendFullBoard(clientManager);
        _turnManagerInstance?.SyncClientTurnState(clientManager);
        _initialisedClientIds.Add(clientManager.PlayerId);
    }

    private static void AddBaseCells(Vector2Int bottomLeft, List<Vector2Int> changedCells)
    {
        changedCells.Add(bottomLeft);
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y));
        changedCells.Add(new Vector2Int(bottomLeft.x, bottomLeft.y + 1));
        changedCells.Add(new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1));
    }

    private static void AddBuildBaseCoreCells(Vector2Int buildWindowOrigin, List<Vector2Int> changedCells)
    {
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 1));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 1, buildWindowOrigin.y + 2));
        changedCells.Add(new Vector2Int(buildWindowOrigin.x + 2, buildWindowOrigin.y + 2));
    }
}
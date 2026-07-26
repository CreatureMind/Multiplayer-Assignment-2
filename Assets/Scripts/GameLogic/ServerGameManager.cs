using System;
using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;

public sealed class ServerGameManager : NetworkBehaviour
{

    [SerializeField] private GameDataSO data;
    private BoardManager _boardManagerInstance;
    private TurnManager _turnManagerInstance;
    private List<ClientManager> _clientManagers = new();

    private bool _boardManagerSpawned;
    private bool _TurnManagerSpawned;
    private bool _ClientManagerSpawned;
    private int _currentPlayerCount;


    private void Awake()
    {
        if (!HasStateAuthority) return;

        EventBus.Subscribe<SceneLoadDoneEvent>(SpawnBoardManager);
        EventBus.Subscribe<SceneLoadDoneEvent>(InstantiateClientManagers); 
        // **note** spawning TurnManager after ClientManagers are instantiated because of dependency so no event sub needed
    }

    private void SpawnTurnManager(SceneLoadDoneEvent _)
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
        _turnManagerInstance.InstantiateTurnManager(_clientManagers, data.TurnStats);
    }

    private void InstantiateClientManagers(SceneLoadDoneEvent _)
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
        
        if (_clientManagers.Count == _currentPlayerCount)
        {
            _ClientManagerSpawned = true;
            SpawnTurnManager(_);
        }
    }


    private void SpawnBoardManager(SceneLoadDoneEvent _)
    {
        if (_boardManagerSpawned)
            return;

        if (!data.BoardManagerPrefab)
        {
            Debug.LogWarning("BoardManager prefab is not assigned.");
            return;
        }

        if (!HasStateAuthority)
            return;

        _boardManagerInstance = Runner.Spawn(data.BoardManagerPrefab, Vector3.zero, Quaternion.identity);
        _boardManagerSpawned = true;
    }

    public void RequestBoardChange(PlayerRef player, Vector2Int gridPosition, TileType targetType)
    {
        if (!HasStateAuthority) return;

        if (!_boardManagerInstance) return;
        
        if (!_turnManagerInstance.ValidatePlayerTurn(player.PlayerId)) return;

        if (!_boardManagerInstance.ValidateBoardChange(gridPosition, targetType)) return;
        
        //implement board change 
    }


    private void OnDestroy()
    {
        EventBus.Unsubscribe<SceneLoadDoneEvent>(SpawnBoardManager);
        EventBus.Unsubscribe<SceneLoadDoneEvent>(InstantiateClientManagers);
    }
}
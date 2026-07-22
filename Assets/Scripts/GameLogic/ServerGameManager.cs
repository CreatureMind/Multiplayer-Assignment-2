using System;
using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ServerGameManager : NetworkBehaviour
{

    [SerializeField] private GameDataSO data;

    private List<ClientManager> _clientManagers = new List<ClientManager>();
    private BoardManager _boardManager;

    private bool _boardManagerSpawned;
    private int _currentPlayerCount;


    private void Awake()
    {
        if (!HasStateAuthority) return;

        EventBus.Subscribe<SceneLoadDoneEvent>(SpawnBoardManager);
        EventBus.Subscribe<SceneLoadDoneEvent>(InstantiateClientManagers);
    }

    private void InstantiateClientManagers(SceneLoadDoneEvent _)
    {
        if (!HasStateAuthority) return;

        var players = Runner.ActivePlayers;

        foreach (var player in players)
        {
            _currentPlayerCount++;
        }

        if (!data.ValidatePlayerCount(_currentPlayerCount))
        {
            throw new InvalidOperationException(
                $"Invalid player count: {_currentPlayerCount}. Expected counts: {string.Join(", ", data.NumberOfPlayers)}");
        }

        foreach (var player in players)
        {
            var clientManager = Runner.Spawn(data.ClientManagerPrefab, Vector3.zero, Quaternion.identity, player);
            clientManager.InstantiateClientManager(this);
            _clientManagers.Add(clientManager);
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

        _boardManager = Runner.Spawn(data.BoardManagerPrefab, Vector3.zero, Quaternion.identity)
            .GetComponent<BoardManager>();
        _boardManagerSpawned = true;
    }

    public void RequestBoardChange(PlayerRef player, Vector2Int gridPosition, TileType targetType)
    {
        if (!HasStateAuthority) return;

        if (!_boardManager) return;
        
        if (!ValidatePlayerTurn(player)) return;

        if (!_boardManager.ValidateBoardChange(gridPosition, targetType)) return;
        
        //implement board change 
    }

    private bool ValidatePlayerTurn(PlayerRef player)
    {
        // Implement player turn validation logic here
        return true;
    }


    private void OnDestroy()
    {
        EventBus.Unsubscribe<SceneLoadDoneEvent>(SpawnBoardManager);
        EventBus.Unsubscribe<SceneLoadDoneEvent>(InstantiateClientManagers);
    }
}
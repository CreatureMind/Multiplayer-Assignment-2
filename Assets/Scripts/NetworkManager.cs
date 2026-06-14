using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System.Linq;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;
    
    [SerializeField] private ReadyManager readyManagerPrefab;
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private PlayerData playerDataPrefab;
    
    public ReadyManager ReadyManagerInstance { get; set; }

    private const int MIN_PLAYERS_TO_START = 2;

    private NetworkRunner _networkRunnerInstance;

    private string _currentLobbyId;
    public string CurrentLobbyId => _currentLobbyId;
    
    private SessionDataRefreshedEvent? _cachedSessionData;
    
    private readonly Dictionary<PlayerRef, PlayerData> _playerDataMap = new();
    
    private void Awake()
    {
        if (!Instance) Instance = this;
        else Destroy(gameObject);
    }

    #region Player Logic
    
    public void RegisterPlayer(PlayerRef player, PlayerData data)
    {
        Debug.Log($"Registering player: {player.ToString()}");
        _playerDataMap[player] = data;
        EventBus.Raise(new PlayerListChangedEvent());
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        Debug.Log($"Unregistering player: {player.ToString()}");
        _playerDataMap.Remove(player);
        EventBus.Raise(new PlayerListChangedEvent());
    }

    public IEnumerable<PlayerData> GetAllPlayers() => _playerDataMap.Values;
    
    public PlayerData GetLocalPlayerData()
    {
        var localPlayer = _networkRunnerInstance.LocalPlayer;
        _playerDataMap.TryGetValue(localPlayer, out var data);
        return data;
    }

    public bool IsLocalPlayer(PlayerRef player) => _networkRunnerInstance.LocalPlayer == player;
    
    public void KickPlayer(PlayerRef player)
    {
        if (!_networkRunnerInstance.IsSharedModeMasterClient) return;
        
        ReadyManagerInstance.KickPlayerRpc(player);
    }

    public bool CanKick() => _networkRunnerInstance.IsSharedModeMasterClient;
    public bool CanStartGame() => _networkRunnerInstance && _networkRunnerInstance.IsSharedModeMasterClient;

    public bool AreAllPlayersReady() =>
        _playerDataMap.Count >= MIN_PLAYERS_TO_START && _playerDataMap.Values.All(p => p.IsReady);
    
    public int GetReadyPlayerCount() => _playerDataMap.Values.Count(p => p.IsReady);
    
    public void SetLocalPlayerReady(bool isReady)
    {
        var data = GetLocalPlayerData();
        if (!data) return;
        data.IsReady = isReady;
    }

    #endregion

    #region Lobby Logic

    public async Task ConnectToCustomLobby(string targetLobbyId)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());
        
        _currentLobbyId = targetLobbyId;
        await CreateFreshRunner();
        
        var result = await _networkRunnerInstance.JoinSessionLobby(SessionLobby.Custom, _currentLobbyId);

        EventBus.Raise(new HideLoadingScreenEvent());
        
        if (result.Ok)
        {
            Debug.Log("Joined lobby successfully!");
            EventBus.Raise(new JoinedLobbyEvent());
            
            if (_cachedSessionData.HasValue)
                EventBus.Raise(_cachedSessionData.Value);
        } 
        else
        {
            Debug.LogError($"Failed to join lobby: {result.ShutdownReason}");
        }
    }
    
    public async Task CreateRoomInCurrentLobby(string roomName, int maxPlayers, string lobbyId)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());
        
        await CreateFreshRunner();
        
        var playerName  = PlayerPrefs.GetString("PlayerName", "Player");
        var sessionName = $"{roomName}_{playerName}_{Guid.NewGuid().ToString("N")[..6]}";
        
        var result = await _networkRunnerInstance.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            CustomLobbyName = lobbyId,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "DisplayName", roomName },
            },
        });
        
        Debug.Log("Creating room...");
        
        EventBus.Raise(new HideLoadingScreenEvent());
        
        if (result.Ok)
        {
            Debug.Log("Created room successfully!");
            
            EventBus.Raise(new RoomCreatedEvent
            {
                RoomName = roomName
            });
        } 
        else
        {
            Debug.LogError($"Failed to create room: {result.ShutdownReason}");
        }
    }

    #endregion

    #region Room Logic
    
    public async Task JoinRoom(string roomName)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());
        
        await CreateFreshRunner();
        
        var result = await _networkRunnerInstance.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
        });
        
        EventBus.Raise(new HideLoadingScreenEvent());
        
        if (result.Ok)
        {
            Debug.Log("Joined room successfully!");
        } 
        else
        {
            Debug.LogError($"Failed to join room: {result.ErrorMessage}, shutdown reason is {result.ShutdownReason}");
        }
    }
    
    public async Task LeaveRoom(string lobbyId)
    {
        await ShutdownRunner();
        await ConnectToCustomLobby(lobbyId);
    }
    
    #endregion

    #region Network Runner

    private async Task CreateFreshRunner()
    {
        await ShutdownRunner();
        
        _networkRunnerInstance = Instantiate(networkRunnerPrefab);
        _networkRunnerInstance.name = "Network_Runner";
        DontDestroyOnLoad(_networkRunnerInstance);
        _networkRunnerInstance.ProvideInput = true;
        _networkRunnerInstance.AddCallbacks(this);
    }

    private async Task ShutdownRunner()
    {
        if (_networkRunnerInstance)
        {
            if (_networkRunnerInstance.IsRunning || !_networkRunnerInstance.IsShutdown)
            {
                await _networkRunnerInstance.Shutdown(destroyGameObject: true);
            }
            else
            {
                Destroy(_networkRunnerInstance.gameObject);
            }
        }
        
        _networkRunnerInstance = null;
    }

    #endregion
    
    #region Callbacks

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.LocalPlayer == player)
        {
            runner.Spawn(playerDataPrefab, inputAuthority: player);

            if (runner.IsSharedModeMasterClient)
                runner.Spawn(readyManagerPrefab);
        }
        
        EventBus.Raise(new PlayerListChangedEvent());
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        _playerDataMap.Remove(player);
        EventBus.Raise(new PlayerListChangedEvent());
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log("ShutDown call because " + shutdownReason);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server!");
    }
    
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log("Session list updated");
        var totalPlayersInThisLobby = 0;
        var validRooms = new List<SessionInfo>();

        foreach (var session in sessionList)
        {
            if (!session.IsValid) continue;
            
            totalPlayersInThisLobby += session.PlayerCount;

            if (session.IsVisible && session.IsOpen)
            {
                validRooms.Add(session);
            }
        }

        _cachedSessionData = new SessionDataRefreshedEvent
        {
            Sessions = validRooms,
            TotalPlayers = totalPlayersInThisLobby
        };
        
        EventBus.Raise(_cachedSessionData.Value);
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    #endregion
    
    private async void OnApplicationQuit()
    {
        try
        {
            await ShutdownRunner();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
}

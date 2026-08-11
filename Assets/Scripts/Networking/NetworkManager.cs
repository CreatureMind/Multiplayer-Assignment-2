using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using Utils;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;

    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private InGameUIManager inGameUIPrefab;

    [Header("Dedicated Server")]
    [SerializeField] private string hubSessionName = "LobbyHub";
    [SerializeField] private string hubLobbyName = "TinySoldiersLobby";
    [SerializeField] private int hubNetSceneBuildIndex = (int)SceneDefs.HUB_NET;
    
    public ChatNetworkManager ChatNetworkManager { get; private set; }

    private const int MIN_PLAYERS_TO_START = 1;

    private NetworkRunner _networkRunnerInstance;
    private bool _handlingDisconnect;

    private string _currentLobbyId;
    public string CurrentLobbyId => _currentLobbyId;
    
    public bool IsReturningFromMatch { get; private set; }
    private bool _unloading = false;
    private InGameUIManager _inGameUIInstance;
    
    public string LocalConfirmedName { get; set; }
    
    private string _pendingOwnerToken;
    
    private RoomCreatedEvent? _pendingCreatedRoom;

    private RoomListChangedEvent? _cachedRoomList;

    private readonly Dictionary<PlayerRef, PlayerData> _playerDataMap = new();

    private void Awake()
    {
#if UNITY_SERVER
        // Client-only manager: the dedicated server never runs it.
        Destroy(gameObject);
        return;
#else
        if (!Instance)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
#endif
    }

    private void Start()
    {
        ChatNetworkManager = GetComponent<ChatNetworkManager>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RoomListChangedEvent>(CacheRoomList);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RoomListChangedEvent>(CacheRoomList);
    }

    private void CacheRoomList(RoomListChangedEvent e) => _cachedRoomList = e;

    #region Player Logic

    public async void RegisterPlayer(PlayerRef player, PlayerData data)
    {
        try
        {
            if (!_playerDataMap.ContainsKey(player))
            {
                Debug.Log($"Registering player: {player.ToString()}");
                _playerDataMap[player] = data;
            }

            await Task.Yield();

            if (data == null || !_networkRunnerInstance || !_networkRunnerInstance.IsRunning) return;

            EventBus.Raise(new PlayerListChangedEvent());
        }
        catch (Exception e)
        {
            Debug.LogException(new Exception($"[Client - NetworkManager] Tried to register {player}: {e.Message}"));
        }
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        if (!Application.isPlaying) return;
        
        Debug.Log($"[Client - NetworkManager] Unregistering player: {player.ToString()}");
        _playerDataMap.Remove(player);

        if (!_networkRunnerInstance || !_networkRunnerInstance.IsRunning || _networkRunnerInstance.IsShutdown) return;
        if (_playerDataMap.Count <= 0) return;

        EventBus.Raise(new PlayerListChangedEvent());
    }

    public IEnumerable<PlayerData> GetAllPlayers() => _playerDataMap.Values;

    public PlayerData GetLocalPlayerData()
    {
        if (!_networkRunnerInstance)
            return null;

        _playerDataMap.TryGetValue(_networkRunnerInstance.LocalPlayer, out var data);
        return data;
    }

    public bool IsLocalPlayer(PlayerRef player) => _networkRunnerInstance && _networkRunnerInstance.LocalPlayer == player;

    public PlayerRef LocalPlayer => _networkRunnerInstance ? _networkRunnerInstance.LocalPlayer : default;
    
    public bool IsRoomOwner() =>
        RoomController.Instance && _networkRunnerInstance &&
        RoomController.Instance.Owner == _networkRunnerInstance.LocalPlayer;

    public void KickPlayer(PlayerRef player)
    {
        if (!IsRoomOwner() || !RoomController.Instance) return;
        RoomController.Instance.Rpc_RequestKick(player, LocalPlayer);
    }

    public void StartMatch()
    {
        if (!IsRoomOwner() || !RoomController.Instance) return;
        RoomController.Instance.Rpc_RequestStartMatch(LocalPlayer);
    }

    public bool CanKick() => IsRoomOwner();
    public bool CanStartGame() => IsRoomOwner();

    // Only read IsReady on spawned objects; a PlayerData mid-transition would throw otherwise.
    private static bool IsReadyAndValid(PlayerData p) => p && p.Object && p.Object.IsValid && p.IsReady;

    public bool AreAllPlayersReady() =>
        _playerDataMap.Count >= MIN_PLAYERS_TO_START && _playerDataMap.Values.All(IsReadyAndValid);

    public int GetReadyPlayerCount() => _playerDataMap.Values.Count(IsReadyAndValid);

    public void SetLocalPlayerReady(bool isReady)
    {
        var data = GetLocalPlayerData();
        if (!data) return;
        data.RequestSetReady(isReady);
    }

    public Dictionary<PlayerRef, PlayerData> GetPlayerDataMap() => _playerDataMap;

    #endregion

    #region Lobby (Hub) Logic
    
    public int GetAllPlayerCount() => _networkRunnerInstance.CommittedPlayers.Count();

    // Play Game -> connect to the server-hosted Lobby Hub as a client.
    public async Task ConnectToCustomLobby(string lobbyName = null)
    {
        // Already in the hub: just re-surface the cached room list.
        if (_networkRunnerInstance && _networkRunnerInstance.IsRunning &&
            _networkRunnerInstance.SessionInfo.Name == hubSessionName)
        {
            EventBus.Raise(new JoinedLobbyEvent());
            if (_cachedRoomList.HasValue)
                EventBus.Raise(_cachedRoomList.Value);
            return;
        }

        EventBus.Raise(new ShowLoadingScreenEvent());

        await CreateFreshRunner();
        _currentLobbyId = hubSessionName;

        var result = await _networkRunnerInstance.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = hubSessionName,
            CustomLobbyName = hubLobbyName,
            EnableClientSessionCreation = false,
            ConnectionToken = ConnectionTokenUtils.Encode(SafeName()),
            SceneManager = _networkRunnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Scene = SceneRef.FromIndex(hubNetSceneBuildIndex),
        });
        
        if (result.Ok)
        {
            Debug.Log("[Client - NetworkManager] Joined Lobby Hub successfully!");
            EventBus.Raise(new JoinedLobbyEvent());
            if (_cachedRoomList.HasValue)
                EventBus.Raise(_cachedRoomList.Value);
        }
        else
        {
            Debug.LogError($"[Client - NetworkManager] Failed to join Lobby Hub: {result.ShutdownReason}");
            
            EventBus.Raise(new ShowDialogEvent(
                title: "Connection Failed",
                message: $"Could not connect to the server: {DescribeShutdown(result.ShutdownReason)}",
                primaryText: "Reconnect",
                onPrimary: () => _ = ConnectToCustomLobby(),
                secondaryText: "Cancel",
                onSecondary: () => EventBus.Raise(new ReturnToMainMenuEvent()),
                type: DialogType.Error
            ));
        }
        
        EventBus.Raise(new HideLoadingScreenEvent());
    }

    // Create a room: ask the server. Approval/rejection comes back via LobbyHubService RPC.
    public Task CreateRoomInCurrentLobby(string roomName, int maxPlayers, string lobbyId, string mode, string map, bool isPublic)
    {
        if (!LobbyHubService.Instance)
        {
            EventBus.Raise(new ShowDialogEvent(
                title: "Not Connected",
                message: "Cannot create room because you are not connected to the lobby hub.",
                primaryText: "Reconnect",
                onPrimary: () => _ = ConnectToCustomLobby(),
                secondaryText: "Cancel",
                onSecondary: null,
                type: DialogType.Warning
            ));

            return Task.CompletedTask;
        }

        EventBus.Raise(new ShowLoadingScreenEvent());

        _pendingCreatedRoom = new RoomCreatedEvent { RoomName = roomName, ModeName = mode, MapName = map , IsPublic = isPublic };

        LobbyHubService.Instance.Rpc_RequestCreateRoom(roomName, mode, map, maxPlayers, isPublic, LocalPlayer);
        return Task.CompletedTask;
    }

    // Called on the requesting client by LobbyHubService when the server approves creation.
    public void OnRoomCreationApproved(string sessionName, string ownerToken)
    {
        _pendingOwnerToken = ownerToken;

        if (_pendingCreatedRoom != null)
        {
            var room = _pendingCreatedRoom.Value;
            room.RoomCode = sessionName;
            _pendingCreatedRoom = room;
        }

        if (_pendingCreatedRoom.HasValue)
            EventBus.Raise(_pendingCreatedRoom.Value);
        _pendingCreatedRoom = null;

        _ = JoinRoom(sessionName);
    }

    // Called on the requesting client by LobbyHubService when the server refuses creation.
    public void OnRoomCreationRejected(string reason)
    {
        _pendingCreatedRoom = null;
        EventBus.Raise(new HideLoadingScreenEvent());
        
        EventBus.Raise(new ShowDialogEvent(
            title: "Room Creation Failed",
            message: reason,
            primaryText: "Retry",
            onPrimary: () => EventBus.Raise(new OpenRoomCreationOverlayEvent()), // Re-opens Room Creation overlay
            secondaryText: "Cancel",
            onSecondary: null,
            type: DialogType.Warning
        ));
    }

    #endregion

    #region Room Logic

    public async Task JoinRoom(string sessionName)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());

        var ownerToken = _pendingOwnerToken;
        _pendingOwnerToken = null;

        await CreateFreshRunner();

        Debug.Log($"[Client - NetworkManager] JoinRoom: connecting to session '{sessionName}' (lobby '{hubLobbyName}')...");

        var result = await _networkRunnerInstance.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            CustomLobbyName = hubLobbyName,
            EnableClientSessionCreation = false,
            ConnectionToken = ConnectionTokenUtils.Encode(SafeName(), ownerToken),
            SceneManager = _networkRunnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Scene = SceneRef.FromIndex(hubNetSceneBuildIndex),
        });

        Debug.Log($"[Client] JoinRoom StartGame: Ok={result.Ok} Reason={result.ShutdownReason}");

        EventBus.Raise(new HideLoadingScreenEvent());

        if (result.Ok)
        {
            Debug.Log("[Client] Joined room successfully!");
            EventBus.Raise(new JoinedRoomEvent());
        }
        else
        {
            Debug.LogError($"[Client] Failed to join room: {result.ShutdownReason}");
            
            EventBus.Raise(new ShowDialogEvent(
                title: "Failed to Join Room",
                message: "Room is closed or not found",
                primaryText: "Refresh Rooms",
                onPrimary: () =>
                {
                    if (_cachedRoomList != null) EventBus.Raise(_cachedRoomList.Value);
                },
                secondaryText: "OK",
                onSecondary: null,
                type: DialogType.Warning
            ));
            
            // Fall back to the hub so the player isn't stranded.
            await ConnectToCustomLobby();
        }
    }

    public async Task LeaveRoom(string lobbyId = null)
    {
        await ShutdownRunner();
        await ConnectToCustomLobby();
    }

    // Leave the match: disconnect, reload the menu scene, rejoin the hub.
    public async Task ReturnToLobbyAsync(float flushDelay = 5f)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());
        
        if (flushDelay > 0f)
            await Task.Delay((int)(flushDelay * 1000));
        
        _inGameUIInstance = null;
        _unloading = false;

        await ShutdownRunner();

        IsReturningFromMatch = true;

        await LoadSceneAsync((int)SceneDefs.MENU);
        await ConnectToCustomLobby();

        IsReturningFromMatch = false;
    }

    private static Task LoadSceneAsync(int buildIndex)
    {
        var tcs = new TaskCompletionSource<bool>();
        var op = SceneManager.LoadSceneAsync(buildIndex);
        if (op != null) op.completed += _ => tcs.TrySetResult(true);
        return tcs.Task;
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
                await _networkRunnerInstance.Shutdown(destroyGameObject: true);
            else
                Destroy(_networkRunnerInstance.gameObject);
        }

        _networkRunnerInstance = null;
        _playerDataMap.Clear();
    }

    private string SafeName() => string.IsNullOrEmpty(LocalConfirmedName) ? "Player" : LocalConfirmedName;

    private static string DescribeShutdown(ShutdownReason reason) => reason switch
    {
        ShutdownReason.GameNotFound => "Server is down.",
        ShutdownReason.GameIsFull => "Room is full.",
        ShutdownReason.ConnectionRefused => "The server refused the connection.",
        ShutdownReason.ConnectionTimeout => "Connection timed out.",
        ShutdownReason.ServerInRoom => "Room is full.",
        ShutdownReason.DisconnectedByPluginLogic => "Disconnected from the server.",
        _ => $"Could not join room ({reason})."
    };

    #endregion

    #region Callbacks

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        EventBus.Raise(new PlayerLeftEvent { Player = player });
        _playerDataMap.Remove(player);

        EventBus.Raise(new PlayerListChangedEvent());
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.Ok)
        {
            Debug.Log("[Client - NetworkManager] Runner shut down cleanly.");
        }
        else
        {
            Debug.Log("[Client - NetworkManager] Disconnected from session. Reason: " + DescribeShutdown(shutdownReason));

            if (shutdownReason == ShutdownReason.DisconnectedByPluginLogic &&
                Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _ = ReturnToLobbyAsync();
                return;
            }

            EventBus.Raise(new ShowDialogEvent(
                title: "Disconnected",
                message: DescribeShutdown(shutdownReason),
                primaryText: "Reconnect",
                onPrimary: () => _ = ConnectToCustomLobby(),
                secondaryText: "Main Menu",
                onSecondary: () => EventBus.Raise(new ReturnToMainMenuEvent()),
                type: DialogType.Error
            ));
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");

        // When kicked (or otherwise server-disconnected) we need to actively return to the hub.
        if (_handlingDisconnect) return;
        if (!_networkRunnerInstance) return;
        if (runner != _networkRunnerInstance) return;
        if (!_networkRunnerInstance.IsRunning) return;
        if (_networkRunnerInstance.SessionInfo.Name == hubSessionName) return;

        _handlingDisconnect = true;
        _ = ReturnToLobbyAsync().ContinueWith(_ => _handlingDisconnect = false,
            TaskScheduler.Current);
    }
    
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Connect failed: {reason}");
        
        EventBus.Raise(new HideLoadingScreenEvent());

        EventBus.Raise(new ShowDialogEvent(
            title: "Connection Failed",
            message: $"Failed to establish network connection: {reason}",
            primaryText: "Retry",
            onPrimary: () => _ = ConnectToCustomLobby(),
            secondaryText: "OK",
            onSecondary: null,
            type: DialogType.Error
        ));
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server!");
    }
    
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"Scene loaded: {SceneManager.GetActiveScene().name}");
        EventBus.Raise(new SceneLoadDoneEvent());

        // In Multiple-Peer mode the game scene merges into Fusion's scene, so the
        // local lobby scene must be unloaded explicitly once the game scene loads.
        if (IsGameSceneLoaded(runner) && !IsReturningFromMatch && !_unloading)
        {
            EventBus.Raise(new GameSceneLoadedEvent());
            _unloading = true;
            StartCoroutine(UnloadLobbySceneNextFrame());
        }
    }

    private static bool IsGameSceneLoaded(NetworkRunner runner)
    {
        if (!runner || !runner.TryGetSceneInfo(out var sceneInfo)) return false;

        var game = SceneRef.FromIndex((int)SceneDefs.GAME);
        for (int i = 0; i < sceneInfo.SceneCount; i++)
            if (sceneInfo.Scenes[i] == game) return true;

        return false;
    }

    private IEnumerator UnloadLobbySceneNextFrame()
    {
        yield return null; // let Fusion finish its scene merge before we touch scenes

        var lobby = SceneManager.GetSceneByBuildIndex((int)SceneDefs.MENU);
        if (lobby.IsValid() && lobby.isLoaded)
        {
            if (SceneManager.GetActiveScene() == lobby)
            {
                var fallback = FindOtherLoadedScene(lobby);
                if (fallback.IsValid()) SceneManager.SetActiveScene(fallback);
            }

            yield return SceneManager.UnloadSceneAsync(lobby);
        }

        SpawnInGameUI();
    }
    
    private void SpawnInGameUI()
    {
        if (!inGameUIPrefab || _inGameUIInstance) return;
        _inGameUIInstance = Instantiate(inGameUIPrefab);
        _inGameUIInstance.name = $"InGameUI_{LocalConfirmedName}";
        _inGameUIInstance.NetworkManager = this;
    }

    private static Scene FindOtherLoadedScene(Scene exclude)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene != exclude) return scene;
        }
        return default;
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        EventBus.Raise(new SceneLoadStartedEvent());
    }

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
    
    #endregion

    #region unused callbacks

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    #endregion
}

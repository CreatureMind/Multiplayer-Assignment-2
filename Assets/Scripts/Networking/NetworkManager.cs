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
    public string LocalConfirmedName { get; set; }

    // Owner token handed back by the server on room creation; presented as the
    // connection token so the room can recognise the creator as its owner.
    private string _pendingOwnerToken;

    // Pending create request, so we can raise RoomCreatedEvent once the server approves.
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
            Debug.LogException(new Exception($"Tried to register {player}: {e.Message}"));
        }
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        if (!Application.isPlaying) return;

        // Always drop the entry, even during shutdown, so a despawned PlayerData can't linger
        // in the map (accessing its networked props later throws "not Spawned").
        Debug.Log($"Unregistering player: {player.ToString()}");
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

    // The server decides owner-only actions; the client only asks.
    public bool IsRoomOwner() =>
        RoomController.Instance && _networkRunnerInstance &&
        RoomController.Instance.Owner == _networkRunnerInstance.LocalPlayer;

    public void KickPlayer(PlayerRef player)
    {
        if (!IsRoomOwner() || !RoomController.Instance) return;
        RoomController.Instance.Rpc_RequestKick(player, LocalPlayer);
    }

    public void StartMatch(string modeName, string mapName)
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
    public async Task ConnectToCustomLobby(string _ = null)
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
            // must be set in Multiple Peer mode, otherwise the scene manager remains "busy" (no scene root), and spawns get stuck.
            Scene = SceneRef.FromIndex(hubNetSceneBuildIndex),
        });

        EventBus.Raise(new HideLoadingScreenEvent());

        if (result.Ok)
        {
            Debug.Log("Joined Lobby Hub successfully!");
            EventBus.Raise(new JoinedLobbyEvent());
            if (_cachedRoomList.HasValue)
                EventBus.Raise(_cachedRoomList.Value);
        }
        else
        {
            Debug.LogError($"Failed to join Lobby Hub: {result.ShutdownReason}");
            EventBus.Raise(new RoomJoinRejectedEvent { Reason = DescribeShutdown(result.ShutdownReason) });
        }
    }

    // Create a room: ask the server. Approval/rejection comes back via LobbyHubService RPC.
    public Task CreateRoomInCurrentLobby(string roomName, int maxPlayers, string lobbyId, string mode, string map, bool isPublic)
    {
        if (!LobbyHubService.Instance)
        {
            EventBus.Raise(new RoomJoinRejectedEvent { Reason = "Not connected to the lobby." });
            return Task.CompletedTask;
        }

        EventBus.Raise(new ShowLoadingScreenEvent());

        _pendingCreatedRoom = new RoomCreatedEvent { RoomName = roomName, ModeName = mode, MapName = map };

        LobbyHubService.Instance.Rpc_RequestCreateRoom(roomName, mode, map, maxPlayers, isPublic, LocalPlayer);
        return Task.CompletedTask;
    }

    // Called on the requesting client by LobbyHubService when the server approves creation.
    public void OnRoomCreationApproved(string sessionName, string ownerToken)
    {
        _pendingOwnerToken = ownerToken;

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
        EventBus.Raise(new RoomJoinRejectedEvent { Reason = reason });
    }

    #endregion

    #region Room Logic

    public async Task JoinRoom(string sessionName)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());

        var ownerToken = _pendingOwnerToken;
        _pendingOwnerToken = null;

        await CreateFreshRunner();

        Debug.Log($"[Client] JoinRoom: connecting to session '{sessionName}' (lobby '{hubLobbyName}')...");

        var result = await _networkRunnerInstance.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            CustomLobbyName = hubLobbyName,
            EnableClientSessionCreation = false,
            ConnectionToken = ConnectionTokenUtils.Encode(SafeName(), ownerToken),
            // Needed so server-driven scene loads (Runner.LoadScene in RoomController) work,
            // but we still avoid forcing MENU as the network scene during the join.
            SceneManager = _networkRunnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            // Same reason as hub join: Multiple Peer mode needs a loaded scene root before runtime spawns work.
            Scene = SceneRef.FromIndex(hubNetSceneBuildIndex),
        });

        Debug.Log($"[Client] JoinRoom StartGame: Ok={result.Ok} Reason={result.ShutdownReason}");

        EventBus.Raise(new HideLoadingScreenEvent());

        if (result.Ok)
        {
            Debug.Log("Joined room successfully!");
        }
        else
        {
            Debug.LogError($"Failed to join room: {result.ShutdownReason}");
            EventBus.Raise(new RoomJoinRejectedEvent { Reason = DescribeShutdown(result.ShutdownReason) });
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
    public async Task ReturnToLobbyAsync(float flushDelay = 0f)
    {
        if (flushDelay > 0f)
            await Task.Delay((int)(flushDelay * 1000));

        // The in-game UI dies with the game scene on the MENU load below; allow the next
        // match to unload the lobby and spawn a fresh UI again.
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
        op.completed += _ => tcs.TrySetResult(true);
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
        ShutdownReason.GameNotFound => "Room no longer exists.",
        ShutdownReason.GameIsFull => "Room is full.",
        ShutdownReason.ConnectionRefused => "The server refused the connection.",
        ShutdownReason.ConnectionTimeout => "Connection timed out.",
        ShutdownReason.ServerInRoom => "Room is full.",
        _ => $"Could not join room ({reason})."
    };

    #endregion

    #region Callbacks

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

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
            Debug.Log("Runner shut down cleanly.");
        }
        else
        {
            Debug.Log("Disconnected from session. Reason: " + shutdownReason);
            // WebGL commonly disconnects when the tab is backgrounded/suspended.
            // Treat it as a transient connection loss and just return to the hub.
            if (shutdownReason == ShutdownReason.DisconnectedByPluginLogic &&
                Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _ = ReturnToLobbyAsync();
                return;
            }

            EventBus.Raise(new RoomJoinRejectedEvent { Reason = DescribeShutdown(shutdownReason) });
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");

        // When kicked (or otherwise server-disconnected) we need to actively return to the hub.
        // Otherwise the client can remain "stuck" in the room UI even though transport is gone.
        if (_handlingDisconnect) return;
        if (!_networkRunnerInstance) return;
        if (runner != _networkRunnerInstance) return;
        if (!_networkRunnerInstance.IsRunning) return;
        if (_networkRunnerInstance.SessionInfo.Name == hubSessionName) return;

        _handlingDisconnect = true;
        _ = ReturnToLobbyAsync().ContinueWith(_ => _handlingDisconnect = false,
            TaskScheduler.Current);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Connect failed: {reason}");
        EventBus.Raise(new RoomJoinRejectedEvent { Reason = reason.ToString() });
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server!");
    }

    // Photon lobby list is unused now; the room list comes from LobbyHubService.
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    private bool _unloading = false;
    private InGameUIManager _inGameUIInstance;
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

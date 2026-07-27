using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

/// <summary>
/// Server-only. Owns every room runner (multi-peer) and creates/closes them on
/// request. One process, many server-mode NetworkRunners — one per room.
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private NetworkRunner roomRunnerPrefab;
    [SerializeField] private RoomController roomControllerPrefab;
    [SerializeField] private PlayerData playerDataPrefab;
    [SerializeField] private ChatRelay chatRelayPrefab;
    [SerializeField] private ServerGameManager serverGameManagerPrefab;

    [SerializeField] private string hubLobbyName = "TinySoldiersLobby";
    [SerializeField] private int maxRooms = 16;
    [SerializeField] private int absoluteMaxPlayersPerRoom = 10;
    [SerializeField] private int absoluteMinPlayersPerRoom = 2;

    private const string DisplayNameProp = "DisplayName";
    private const string ModeNameProp = "ModeName";
    private const string MapNameProp = "MapName";

    private readonly Dictionary<int, RoomRuntime> _rooms = new();
    private int _nextRoomId = 1;

    public struct RoomCreateResult
    {
        public bool Ok;
        public string SessionName;
        public string OwnerToken;
        public string Reason;
        public RoomInfo Info;
    }

    private class RoomRuntime
    {
        public NetworkRunner Runner;
        public RoomRunnerCallbacks Callbacks;
        public bool BootstrapStarted;
        public bool BootstrapDone;
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private static async Task<T> SpawnAndWaitAsync<T>(NetworkRunner runner, T prefab, float timeoutSeconds = 3f)
        where T : NetworkBehaviour
    {
        var tcs = new TaskCompletionSource<T>();

        runner.Spawn(prefab, onBeforeSpawned: (_, obj) => tcs.TrySetResult(obj.GetComponent<T>()));

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var finished = await Task.WhenAny(tcs.Task, timeoutTask);

        return finished == tcs.Task ? tcs.Task.Result : null;
    }

    public async Task<RoomCreateResult> CreateRoomAsync(string roomName, string mode, string map,
        int maxPlayers, bool isPublic, PlayerRef requester)
    {
        var cleanRoomName = Sanitize(roomName);
        
        Debug.Log($"[Server] CreateRoomAsync: name='{cleanRoomName}' mode='{mode}' map='{map}' max={maxPlayers} public={isPublic} requester={requester}");

        if (!roomRunnerPrefab)
        {
            Debug.LogError("[Server] roomRunnerPrefab NOT assigned!");
            return Fail("Server misconfigured (runner).");
        }

        if (!roomControllerPrefab)
        {
            Debug.LogError("[Server] roomControllerPrefab NOT assigned!");
            return Fail("Server misconfigured (controller).");
        }

        if (!playerDataPrefab)
        {
            Debug.LogError("[Server] playerDataPrefab NOT assigned!");
            return Fail("Server misconfigured (playerData).");
        }

        if (_rooms.Count >= maxRooms)
            return Fail("Server is full — no room slots available.");

        maxPlayers = Mathf.Clamp(maxPlayers, absoluteMinPlayersPerRoom, absoluteMaxPlayersPerRoom);

        var roomId = _nextRoomId++;
        var sessionName = LobbyCatalog.SessionNameFor(roomId);
        var ownerToken = Guid.NewGuid().ToString("N")[..8];

        var runner = Instantiate(roomRunnerPrefab);
        runner.name = $"Room_Runner_{sessionName}";

        var callbacks = runner.gameObject.AddComponent<RoomRunnerCallbacks>();
        callbacks.Init(this, roomId, sessionName, maxPlayers, ownerToken, playerDataPrefab, roomControllerPrefab);
        runner.AddCallbacks(callbacks);

        Debug.Log($"[Server] Room '{sessionName}': starting server runner (lobby '{hubLobbyName}')...");

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Server,
            SessionName = sessionName,
            CustomLobbyName = hubLobbyName,
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Scene = SceneRef.FromIndex((int)SceneDefs.HUB_NET),
            PlayerCount = maxPlayers,
            IsVisible = isPublic,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { DisplayNameProp, cleanRoomName },
                { ModeNameProp, mode },
                { MapNameProp, map },
            }
        });

        Debug.Log($"[Server] Room '{sessionName}' StartGame: Ok={result.Ok} Reason={result.ShutdownReason}");

        if (!result.Ok)
        {
            Destroy(runner.gameObject);
            return Fail($"Failed to start room ({result.ShutdownReason}).");
        }

        _rooms[roomId] = new RoomRuntime { Runner = runner, Callbacks = callbacks };

        // Spawn the room's authority object now that the runner is up (no scene load to wait on).
        var roomController = await SpawnAndWaitAsync(runner, roomControllerPrefab);

        if (!roomController)
        {
            Debug.LogError($"[Server] Room '{sessionName}' controller never appeared. Shutting room down.");
            _ = runner.Shutdown(destroyGameObject: true);
            return Fail("Server failed to initialize room.");
        }

        roomController.ServerInit(roomId);
        callbacks.SetController(roomController);
        Debug.Log($"[Server] Room '{sessionName}' controller spawned={(roomController ? "yes" : "NULL")}. Active rooms={_rooms.Count}");

        if (!chatRelayPrefab)
        {
            Debug.LogWarning($"[Server] Room '{sessionName}': chatRelayPrefab not assigned; room chat will be unavailable.");
        }
        else
        {
            var relay = runner.Spawn(chatRelayPrefab);
            if (relay)
                relay.name = $"ChatRelay_{sessionName}";

            var announcer = runner.GetComponent<RunnerChatAnnouncer>();
            if (!announcer) announcer = runner.gameObject.AddComponent<RunnerChatAnnouncer>();
            announcer.SetRelay(relay);
            callbacks.SetAnnouncer(announcer);
        }

        var info = new RoomInfo
        {
            RoomId = roomId,
            DisplayName = cleanRoomName,
            ModeId = LobbyCatalog.ModeId(mode),
            MapId = LobbyCatalog.MapId(map),
            PlayerCount = 0,
            MaxPlayers = (byte)maxPlayers,
            IsOpen = true,
            IsPublic = isPublic,
            Owner = default
        };

        return new RoomCreateResult
        {
            Ok = true,
            SessionName = sessionName,
            OwnerToken = ownerToken,
            Reason = string.Empty,
            Info = info
        };
    }

    // ---------------- Registry passthrough (server-side) ----------------

    public void SetRoomPlayerCount(int roomId, int count) =>
        LobbyHubService.Instance?.SetRoomPlayerCount(roomId, count);

    public void SetRoomOpen(int roomId, bool isOpen) =>
        LobbyHubService.Instance?.SetRoomOpen(roomId, isOpen);

    public void SetRoomOwner(int roomId, PlayerRef owner) =>
        LobbyHubService.Instance?.SetRoomOwner(roomId, owner);

    public void CloseRoom(int roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;
        if (room.Runner && !room.Runner.IsShutdown)
            _ = room.Runner.Shutdown(destroyGameObject: true);
        // Registry cleanup happens in OnRoomRunnerShutdown.
    }

    public void OnRoomRunnerShutdown(int roomId)
    {
        LobbyHubService.Instance?.RemoveRoom(roomId);
        _rooms.Remove(roomId);
    }

    private static RoomCreateResult Fail(string reason) => new()
    {
        Ok = false, SessionName = string.Empty, OwnerToken = string.Empty, Reason = reason
    };

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Room";
        var cleaned = raw.Trim().Replace(" ", "_");
        return cleaned.Length > 16 ? cleaned[..16] : cleaned;
    }
    
    public void TryBeginPostGameSceneBootstrap(int roomId, NetworkRunner runner)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;
        if (!runner || room.Runner != runner) return;
        if (room.BootstrapStarted || room.BootstrapDone) return;

        room.BootstrapStarted = true;
        StartCoroutine(BootstrapAfterSceneLoadRoutine(room));
    }

    private IEnumerator BootstrapAfterSceneLoadRoutine(RoomRuntime room)
    {
        if (!room.Runner || room.Runner.IsShutdown)
        {
            room.BootstrapStarted = false;
            yield break;
        }
        Debug.Log($"[Server] Room '{room.Runner.SessionInfo.Name}' bootstrap gate opened. Waiting for Clean_Game_Scene to settle...");

        GameObject requiredComponents;
        SceneRef loadedScene;
        var timeoutAt = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < timeoutAt)
        {
            if (!room.Runner || room.Runner.IsShutdown)
            {
                room.BootstrapStarted = false;
                yield break;
            }
            requiredComponents = FindAnyObjectByType<ClientSceneContext>()?.gameObject; 
            loadedScene = room.Runner.SceneManager.GetSceneRef(requiredComponents);
            if (loadedScene.IsValid)
                break;

            yield return null;
        }
        requiredComponents = FindAnyObjectByType<ClientSceneContext>()?.gameObject;
        loadedScene = room.Runner.SceneManager.GetSceneRef(requiredComponents);
        
        if (!loadedScene.IsValid)
        {
            room.BootstrapStarted = false;
            Debug.LogWarning($"[Server] Room '{room.Runner.SessionInfo.Name}' bootstrap gate timed out waiting for Clean_Game_Scene. Will retry on next scene load callback.");
            yield break;
        }

        // Defer one frame so post-load objects and runner bookkeeping settle before spawning authoritative game logic.
        yield return null;

        if (!room.Runner || room.Runner.IsShutdown)
        {
            room.BootstrapStarted = false;
            yield break;
        }

        if (room.BootstrapDone)
            yield break;

        room.BootstrapDone = true;
        Debug.Log($"[Server] Room '{room.Runner.SessionInfo.Name}' bootstrap gate passed. Spawning ServerGameManager...");
        BootServerGameManager(room.Runner);
    }
public void BootServerGameManager(NetworkRunner runner)
    {
        if (!runner) return;

        if (!serverGameManagerPrefab)
        {
            Debug.LogWarning($"[Server] Room '{runner.SessionInfo.Name}': serverGameManagerPrefab not assigned; game logic will be unavailable.");
        }
        else
        {
            runner.Spawn(serverGameManagerPrefab);
        }
    }
    
}

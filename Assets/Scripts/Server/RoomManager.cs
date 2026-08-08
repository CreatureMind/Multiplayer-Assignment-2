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
    [SerializeField] private int maxRooms = 5;
    [SerializeField] private int absoluteMaxPlayersPerRoom = 4;
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
        var sessionName = GetUniqueToken();
        var ownerToken = GetUniqueToken();

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
            {
                relay.name = $"ChatRelay_{sessionName}";
                // One relay serves the room for its whole life (lobby + match), so keep it
                // out of the scene that the Single game-scene load unloads. In Multiple-Peer
                // mode the spawned object is parented under the scene root, so detach it first
                // (MakeDontDestroyOnLoad asserts the object is unparented before reparenting it).
                relay.transform.SetParent(null, true);
                runner.MakeDontDestroyOnLoad(relay.gameObject);
            }

            var announcer = runner.GetComponent<RunnerChatAnnouncer>();
            if (!announcer) announcer = runner.gameObject.AddComponent<RunnerChatAnnouncer>();
            announcer.SetRelay(relay);
            callbacks.SetAnnouncer(announcer);
        }
        
        if (!serverGameManagerPrefab)
        {
            Debug.LogWarning($"[Server] Room '{sessionName}': serverGameManagerPrefab not assigned; server game manager will be unavailable.");
        }
        else
        {
            var gM = runner.Spawn(serverGameManagerPrefab);
            if (gM)
            {
                gM.name = $"ServerGameManager_{sessionName}";
                // One relay serves the room for its whole life (lobby + match), so keep it
                // out of the scene that the Single game-scene load unloads.
                gM.transform.SetParent(null, true);
                runner.MakeDontDestroyOnLoad(gM.gameObject);
                gM.InstantiateMap(map);
            }
        }

        var info = new RoomInfo
        {
            SessionName = sessionName,
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
    
    private string GetUniqueToken() => Guid.NewGuid().ToString("N").ToUpper()[..8];
}

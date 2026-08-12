using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

/// <summary>
/// Entry point in the LAUNCH scene. On a dedicated server build it starts the
/// persistent Lobby Hub session (which owns room creation); on a client build it
/// simply proceeds to the MENU scene and lets the normal client flow take over.
/// </summary>
[RequireComponent(typeof(RoomManager))]
public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private LobbyHubService lobbyHubServicePrefab;
    [SerializeField] private ChatRelay chatRelayPrefab;
    [SerializeField] private PlayerData hubPlayerDataPrefab;
    [SerializeField] private string hubSessionName = "LobbyHub";
    [SerializeField] private string hubLobbyName = "TinySoldiersLobby";
    [SerializeField] private int maxLobbyPlayers = 200;
    [SerializeField] private int hubNetSceneBuildIndex = (int)SceneDefs.HUB_NET;
    [SerializeField, Range(30,120)] private int targetFPS = 30;

    private async void Start()
    {
        await Task.Yield();

#if UNITY_SERVER
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
        DontDestroyOnLoad(gameObject);
        await StartHub();
#else
        SceneManager.LoadScene((int)SceneDefs.MENU, LoadSceneMode.Single);
#endif
    }

    private async Task StartHub()
    {
        Debug.Log("[Server] StartHub begin.");

        if (!runnerPrefab)
        {
            Debug.LogError("[Server] runnerPrefab NOT assigned!");
            return;
        }

        if (!lobbyHubServicePrefab)
        {
            Debug.LogError("[Server] lobbyHubServicePrefab NOT assigned!");
            return;
        }

        Debug.Log($"[Server] RoomManager.Instance={(RoomManager.Instance ? "set" : "NULL")}");

        var runner = Instantiate(runnerPrefab);
        runner.name = "Hub_Runner";
        runner.ProvideInput = false;
        DontDestroyOnLoad(runner.gameObject);

        Debug.Log($"[Server] Starting Hub session '{hubSessionName}' (lobby '{hubLobbyName}')...");

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Server,
            SessionName = hubSessionName,
            CustomLobbyName = hubLobbyName,
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Scene = SceneRef.FromIndex(hubNetSceneBuildIndex),
            PlayerCount = maxLobbyPlayers,
            IsVisible = false
        });

        Debug.Log($"[Server] Hub StartGame: Ok={result.Ok} Reason={result.ShutdownReason}");

        if (!result.Ok)
        {
            Debug.LogError($"[Server] Failed to start Lobby Hub: {result.ShutdownReason}");
            Application.Quit(1);
            return;
        }

        // Server-side chat system messages + name tracking in the hub.
        var announcer = runner.GetComponent<RunnerChatAnnouncer>();
        if (!announcer) announcer = runner.gameObject.AddComponent<RunnerChatAnnouncer>();

        var hub = runner.Spawn(lobbyHubServicePrefab);
        if (hub)
          DontDestroyOnLoad(hub.gameObject);

        if (!chatRelayPrefab)
        {
            Debug.LogWarning("[Server] chatRelayPrefab not assigned; lobby chat will be unavailable.");
        }
        else
        {
            var relay = runner.Spawn(chatRelayPrefab);
            if (relay)
                relay.name = "ChatRelay_Hub";
            announcer.SetRelay(relay);
        }

        var hubCallbacks = runner.GetComponent<HubRunnerCallbacks>();
        if (!hubCallbacks) hubCallbacks = runner.gameObject.AddComponent<HubRunnerCallbacks>();
        if (hubPlayerDataPrefab)
            hubCallbacks.Init(hubPlayerDataPrefab);
        else
            Debug.LogWarning("[Server] hubPlayerDataPrefab not assigned; hub players will have no names.");
        runner.AddCallbacks(hubCallbacks);

        Debug.Log($"[Server] Hub Spawn returned '{(hub ? hub.name : "NULL")}'. Lobby Hub '{hubSessionName}' online " +
                  $"(LobbyHubService.Instance set confirmed in LobbyHubService.Spawned).");
    }
}

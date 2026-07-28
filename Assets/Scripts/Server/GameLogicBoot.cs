using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLogicBoot : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private ServerGameManager _serverGameManagerPrefab;
    [SerializeField] private float _runnerResolveTimeoutSeconds = 30f;
    [SerializeField] private float _readinessTimeoutSeconds = 10f;
    [SerializeField] private float _pollIntervalSeconds = 0.2f;

    private const int RequiredValidPlayerCount = 4;

    private NetworkRunner _runner;
    private bool _callbacksRegistered;
    private bool _bootstrapStarted;

    private void Awake()
    {
#if UNITY_SERVER
        return;
#endif
        Destroy(gameObject);
    }

    private void Start()
    {
#if UNITY_SERVER
        StartCoroutine(ServerStartupRoutine());
#endif
    }

    private void OnDestroy()
    {
        if (_callbacksRegistered && _runner)
            _runner.RemoveCallbacks(this);
    }

    private IEnumerator ServerStartupRoutine()
    {
        Debug.Log($"[GameLogicBoot] Startup begin on scene '{gameObject.scene.name}'.");

        yield return ResolveRunnerRoutine();
        if (!_runner)
            yield break;

        _runner.AddCallbacks(this);
        _callbacksRegistered = true;

        Debug.Log($"[GameLogicBoot] Bound runner='{_runner.name}' scene='{GetRunnerSceneName(_runner)}'. Waiting for scene/player readiness...");

        yield return WaitForReadinessRoutine();
        EvaluateBootstrapGate("startup-ready");
    }

    private IEnumerator ResolveRunnerRoutine()
    {
        var elapsed = 0f;
        string lastReason = string.Empty;

        while (elapsed < _runnerResolveTimeoutSeconds)
        {
            if (TryResolveRunner(out var resolved, out var reason))
            {
                _runner = resolved;
                Debug.Log($"[GameLogicBoot] Runner selected: '{_runner.name}', scene='{GetRunnerSceneName(_runner)}'.");
                yield break;
            }

            lastReason = reason;

            if (Mathf.Approximately(elapsed, 0f))
                Debug.Log($"[GameLogicBoot] Waiting for runner: {reason}");

            yield return new WaitForSeconds(_pollIntervalSeconds);
            elapsed += _pollIntervalSeconds;
        }

        Debug.LogError($"[GameLogicBoot] Failed to resolve runner within {_runnerResolveTimeoutSeconds:F1}s. Last reason: {lastReason}");
    }

    private bool TryResolveRunner(out NetworkRunner resolved, out string reason)
    {
        resolved = null;
        reason = string.Empty;

        var allRunners = FindObjectsOfType<NetworkRunner>();
        var serverRunners = allRunners.Where(r => r && r.IsServer).ToArray();

        if (serverRunners.Length == 0)
        {
            reason = "no server runners available yet";
            return false;
        }

        if (serverRunners.Length == 1)
        {
            resolved = serverRunners[0];
            reason = "single server runner found";
            return true;
        }

        var sceneMatched = serverRunners
            .Where(r => r.SceneManager != null &&
                        r.SceneManager.MainRunnerScene.IsValid() &&
                        r.SceneManager.MainRunnerScene == gameObject.scene)
            .ToArray();

        if (sceneMatched.Length == 1)
        {
            resolved = sceneMatched[0];
            reason = "matched runner by scene";
            return true;
        }

        reason = $"multiple server runners ({serverRunners.Length}) and no unique scene match yet";
        return false;
    }

    private IEnumerator WaitForReadinessRoutine()
    {
        var elapsed = 0f;
        var stableSamples = 0;
        int? lastValidCount = null;

        Debug.Log($"[GameLogicBoot] Readiness wait start. timeout={_readinessTimeoutSeconds:F1}s poll={_pollIntervalSeconds:F2}s");

        while (elapsed < _readinessTimeoutSeconds)
        {
            if (!_runner || _runner.IsShutdown)
            {
                Debug.LogError("[GameLogicBoot] Runner became invalid during readiness wait.");
                yield break;
            }

            var rawPlayers = _runner.ActivePlayers.ToList();
            var validPlayers = rawPlayers.Where(IsValidPlayer).ToList();

            var sceneReady = _runner.SceneManager != null &&
                             _runner.SceneManager.MainRunnerScene.IsValid() &&
                             _runner.SceneManager.MainRunnerScene == gameObject.scene;

            if (sceneReady)
            {
                if (lastValidCount.HasValue && lastValidCount.Value == validPlayers.Count)
                    stableSamples++;
                else
                    stableSamples = 0;

                lastValidCount = validPlayers.Count;

                if (stableSamples >= 2)
                {
                    Debug.Log($"[GameLogicBoot] Readiness complete. raw={rawPlayers.Count} [{FormatPlayers(rawPlayers)}], valid={validPlayers.Count} [{FormatPlayers(validPlayers)}]");
                    yield break;
                }
            }

            if ((int)(elapsed / _pollIntervalSeconds) % 5 == 0)
            {
                Debug.Log($"[GameLogicBoot] Readiness waiting... sceneReady={sceneReady}, raw={rawPlayers.Count} [{FormatPlayers(rawPlayers)}], valid={validPlayers.Count} [{FormatPlayers(validPlayers)}]");
            }

            yield return new WaitForSeconds(_pollIntervalSeconds);
            elapsed += _pollIntervalSeconds;
        }

        Debug.LogWarning("[GameLogicBoot] Readiness wait timed out; proceeding with current snapshot.");
    }

    private void EvaluateBootstrapGate(string source)
    {
        if (_bootstrapStarted || !_runner || _runner.IsShutdown)
            return;

        var rawPlayers = _runner.ActivePlayers.ToList();
        var validPlayers = rawPlayers.Where(IsValidPlayer).ToList();

        Debug.Log($"[GameLogicBoot] Gate '{source}': raw={rawPlayers.Count} [{FormatPlayers(rawPlayers)}], valid={validPlayers.Count} [{FormatPlayers(validPlayers)}], requiredValid={RequiredValidPlayerCount}.");

        if (validPlayers.Count != RequiredValidPlayerCount)
        {
            Debug.Log($"[GameLogicBoot] Gate blocked at '{source}': valid player count must be exactly {RequiredValidPlayerCount}.");
            return;
        }

        _bootstrapStarted = true;
        Debug.Log($"[GameLogicBoot] Gate passed at '{source}'. Bootstrapping ServerGameManager.");
        StartCoroutine(BootServerRoutine());
    }

    private static bool IsValidPlayer(PlayerRef player) => player.PlayerId != -1;

    private static string FormatPlayers(IEnumerable<PlayerRef> players)
    {
        var ids = players.Select(p => p.PlayerId).ToArray();
        return ids.Length == 0 ? "<none>" : string.Join(",", ids);
    }

    private static string GetRunnerSceneName(NetworkRunner runner)
    {
        if (!runner || runner.SceneManager == null)
            return "<none>";

        var scene = runner.SceneManager.MainRunnerScene;
        return scene.IsValid() ? scene.name : "<invalid>";
    }

    private IEnumerator BootServerRoutine()
    {
        if (!_serverGameManagerPrefab)
        {
            Debug.LogError("[GameLogicBoot] ServerGameManager prefab is not assigned.");
            yield break;
        }
        
        if (!_runner)
        {
            Debug.LogError("[GameLogicBoot] NetworkRunner is not assigned.");
            yield break;
        }
        
        var sgm = _runner.Spawn(_serverGameManagerPrefab);
        
        yield return new WaitForSeconds(3f);
        
        if (!sgm)
        {
            Debug.LogError("[GameLogicBoot] Failed to spawn ServerGameManager.");
            yield break;
        }
        
        Debug.Log("[GameLogicBoot] ServerGameManager Spawned Hallelujah");
        
        Destroy(gameObject);
    }

    #region Unused Callbacks
    
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }
    
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner != _runner)
            return;

        if (!IsValidPlayer(player))
        {
            Debug.Log("[GameLogicBoot] OnPlayerJoined ignored invalid player ref (-1).");
            return;
        }

        Debug.Log($"[GameLogicBoot] OnPlayerJoined: {player.PlayerId}");
        EvaluateBootstrapGate("OnPlayerJoined");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner != _runner)
            return;

        Debug.Log($"[GameLogicBoot] OnPlayerLeft: {player.PlayerId}");
        EvaluateBootstrapGate("OnPlayerLeft");
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }
    
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }
    
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }
    
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }
    
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }
    
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }
    
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }
    
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
    }
    
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }
    
    public void OnConnectedToServer(NetworkRunner runner)
    {
    }
    
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }
    
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }
    
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }
    
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner != _runner)
            return;

        Debug.Log($"[GameLogicBoot] OnSceneLoadDone: runnerScene='{GetRunnerSceneName(runner)}' objectScene='{gameObject.scene.name}'.");
        EvaluateBootstrapGate("OnSceneLoadDone");
    }
    
    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
    
    #endregion
}
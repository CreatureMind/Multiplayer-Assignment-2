using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Server-only callbacks for the persistent LobbyHub runner.
/// Spawns PlayerData so clients have display names in the hub (for chat and UI).
/// </summary>
public class HubRunnerCallbacks : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private PlayerData playerDataPrefab;

    private readonly Dictionary<PlayerRef, PlayerData> _players = new();

    public void Init(PlayerData prefab) => playerDataPrefab = prefab;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        if (!playerDataPrefab)
        {
            Debug.LogError("[Server] HubRunnerCallbacks: playerDataPrefab not assigned.");
            return;
        }

        ConnectionTokenUtils.Decode(runner.GetPlayerConnectionToken(player), out var displayName, out _);
        var pd = runner.Spawn(playerDataPrefab, inputAuthority: player);
        if (pd)
        {
            pd.ServerInitialize(displayName);
            _players[player] = pd;
        }

        var announcer = runner.GetComponent<RunnerChatAnnouncer>();
        announcer?.AnnounceJoined(displayName);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        string displayName = string.Empty;
        if (_players.TryGetValue(player, out var pd) && pd)
        {
            displayName = pd.DisplayName.ToString();
            if (pd.Object)
                runner.Despawn(pd.Object);
        }
        _players.Remove(player);

        var announcer = runner.GetComponent<RunnerChatAnnouncer>();
        announcer?.AnnounceLeft(displayName);
    }

    #region Unused callbacks
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, Fusion.Sockets.NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, Fusion.Sockets.NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    #endregion
}


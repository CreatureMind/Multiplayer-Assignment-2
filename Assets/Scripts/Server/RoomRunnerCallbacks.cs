using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

/// <summary>
/// Runner-level callbacks for a single room session on the server. Enforces the
/// capacity gate on connection, spawns/despawns PlayerData, assigns the owner and
/// keeps the Lobby Hub registry in sync with the room's player count.
/// </summary>
public class RoomRunnerCallbacks : MonoBehaviour, INetworkRunnerCallbacks
{
    private RoomManager _manager;
    private int _roomId;
    private string _sessionName;
    private int _maxPlayers;
    private string _ownerToken;
    private PlayerData _playerDataPrefab;
    private RoomController _roomControllerPrefab;
    private RoomController _roomController;
    private RunnerChatAnnouncer _announcer;
    private bool _ownerAssigned;
    private PlayerRef _pendingOwner;
    private bool _hasPendingOwner;

    private readonly Dictionary<PlayerRef, PlayerData> _players = new();

    public void Init(RoomManager manager, int roomId, string sessionName, int maxPlayers, string ownerToken,
        PlayerData playerDataPrefab, RoomController roomControllerPrefab)
    {
        _manager = manager;
        _roomId = roomId;
        _sessionName = sessionName;
        _maxPlayers = maxPlayers;
        _ownerToken = ownerToken;
        _playerDataPrefab = playerDataPrefab;
        _roomControllerPrefab = roomControllerPrefab;
    }

    public void SetAnnouncer(RunnerChatAnnouncer announcer) => _announcer = announcer;

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        var active = runner.ActivePlayers.Count();
        Debug.Log($"[Server] Room '{_sessionName}' OnConnectRequest: active={active}/{_maxPlayers}, matchStarted={(_roomController && _roomController.MatchStarted)}.");

        // this is the "server is full" rejection point.
        if (active >= _maxPlayers)
        {
            Debug.Log($"[Server] Room '{_sessionName}' REFUSED: full.");
            request.Refuse();
            return;
        }

        // No joining a match already in progress.
        if (_roomController && _roomController.MatchStarted)
        {
            Debug.Log($"[Server] Room '{_sessionName}' REFUSED: match in progress.");
            request.Refuse();
            return;
        }

        Debug.Log($"[Server] Room '{_sessionName}' ACCEPTED connection.");
        request.Accept();
    }

    // Called by RoomManager once the controller is spawned into this runner.
    public void SetController(RoomController controller)
    {
        _roomController = controller;
        TryAssignPendingOwner();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        ConnectionTokenUtils.Decode(runner.GetPlayerConnectionToken(player), out var displayName, out var playerToken);
        Debug.Log($"[Server] Room '{_sessionName}' player joined {player}: name='{displayName}' token='{playerToken}'.");

        var playerData = runner.Spawn(_playerDataPrefab, inputAuthority: player);
        playerData.ServerInitialize(displayName);
        _players[player] = playerData;

        _announcer?.AnnounceJoined(displayName);

        // The creator (matching owner token) becomes owner; fall back to first joiner.
        if (!_ownerAssigned && !_hasPendingOwner &&
            (playerToken == _ownerToken || string.IsNullOrEmpty(_ownerToken)))
        {
            _pendingOwner = player;
            _hasPendingOwner = true;
            TryAssignPendingOwner();
        }

        _manager?.SetRoomPlayerCount(_roomId, runner.ActivePlayers.Count());
    }

    private void TryAssignPendingOwner()
    {
        if (_ownerAssigned || !_hasPendingOwner || !_roomController) return;
        _ownerAssigned = true;
        _roomController.AssignOwner(_pendingOwner);
        _manager?.SetRoomOwner(_roomId, _pendingOwner);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        var leftName = string.Empty;
        if (_players.TryGetValue(player, out var playerData) && playerData && playerData.Object)
        {
            leftName = playerData.DisplayName.ToString();
            runner.Despawn(playerData.Object);
        }
        _players.Remove(player);

        _announcer?.AnnounceLeft(leftName);

        var remaining = runner.ActivePlayers.Where(p => p != player).ToList();

        // if owner left hand ownership to someone still here, or close if empty.
        if (_roomController && _roomController.Owner == player && remaining.Count > 0)
        {
            var next = remaining[0];
            _roomController.AssignOwner(next);
            _manager?.SetRoomOwner(_roomId, next);
        }

        if (remaining.Count == 0)
        {
            _manager?.CloseRoom(_roomId);
            return;
        }

        _manager?.SetRoomPlayerCount(_roomId, remaining.Count);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Server] Room '{_sessionName}' runner shutdown: {shutdownReason}.");
        _manager?.OnRoomRunnerShutdown(_roomId);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (SceneManager.GetSceneByBuildIndex((int)SceneDefs.MENU).isLoaded)
            SceneManager.UnloadSceneAsync((int)SceneDefs.MENU);
    }

    #region Unused callbacks

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"[Server] Room '{_sessionName}' OnDisconnectedFromServer: {reason}."); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.Log($"[Server] Room '{_sessionName}' OnConnectFailed: {reason}."); }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    #endregion
}

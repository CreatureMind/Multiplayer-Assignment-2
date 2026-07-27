using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Utils {

  public class ServerEventsInfo : SimulationBehaviour, INetworkRunnerCallbacks {

    private const int TIMEOUT = 5;
    private float TIME_COUNTER = TIMEOUT;

    private void Update() {

      TIME_COUNTER -= Time.deltaTime;

      if (TIME_COUNTER < 0) {
        TIME_COUNTER = TIMEOUT;

        if (Runner != null && Runner.IsServer) {
          var msg = $"Total Players: {Runner.ActivePlayers.Count()}";

          foreach (var player in Runner.ActivePlayers) {
            msg += $"\n{player}: {Runner.GetPlayerConnectionType(player)}";
          }

          Debug.Log(msg);
        }
      }
    }

    private void OnDestroy() {
      Log.Debug($"{nameof(OnDestroy)}: {gameObject.name}");
    }

    public void OnConnectedToServer(NetworkRunner runner) {
      Log.Debug($"{nameof(OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
      Log.Debug($"{nameof(OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
      Log.Debug($"{nameof(OnConnectRequest)}: {nameof(request.RemoteAddress)}: {request.RemoteAddress}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
      Log.Debug($"{nameof(OnDisconnectedFromServer)}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer} due to {reason}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
      Log.Debug($"{nameof(OnPlayerJoined)}: {nameof(player)}: {player}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
      Log.Debug($"{nameof(OnPlayerLeft)}: {nameof(player)}: {player}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
      Log.Debug($"{nameof(OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");
    }


    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
      Log.Debug($"{nameof(OnSessionListUpdated)}: {nameof(sessionList)}: {sessionList.Count}");
    }

    public void OnSceneLoadDone(NetworkRunner runner) {
      Log.Debug($"{nameof(OnSceneLoadDone)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene}");
    }

    public void OnSceneLoadStart(NetworkRunner runner) {
      Log.Debug($"{nameof(OnSceneLoadStart)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene}");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {
      Log.Debug($"{nameof(OnCustomAuthenticationResponse)}");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {
      Log.Debug($"{nameof(OnReliableDataReceived)}: {nameof(PlayerRef)}:{player}, {nameof(data)}:{data.Count}");
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
      Log.Debug($"{nameof(OnHostMigration)}: {nameof(HostMigrationToken)}: {hostMigrationToken}");
    }
    
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
  }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class GameLogicBoot : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private ServerGameManager _serverGameManagerPrefab;
    [SerializeField] private GameDataSO gameData;
    private NetworkRunner _runner;
    
    private HashSet<PlayerRef> _joinedPlayers = new HashSet<PlayerRef>();
    private int _sceneClientCount = 0;

    private void Awake()
    {
        
#if UNITY_SERVER 
        _runner = FindAnyObjectByType<NetworkRunner>();
        if (!_runner)
        {
            Debug.LogError($"[GameLogicBoot] Couldn't find NetworkRunner component.");
            return;
        }

        _runner.AddCallbacks(this);

        var active = _runner.ActivePlayers;

        foreach (var player in active)
        {
            if (player.PlayerId == -1)
                continue;
            _joinedPlayers.Add(player);
            LocalOnPlayerJoined();
        }
        return;
#endif
        Destroy(gameObject);
    }

    private void LocalOnPlayerJoined()
    {
        _sceneClientCount++;
    
        if (_sceneClientCount == _joinedPlayers.Count && gameData.ValidatePlayerCount(_sceneClientCount))
        {
            StartCoroutine(BootServerRoutine());
        }
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
        if (player.PlayerId == -1)
            return;
        if (!_joinedPlayers.Add(player))
            return;

        LocalOnPlayerJoined();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
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
    }
    
    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
    
    #endregion
}
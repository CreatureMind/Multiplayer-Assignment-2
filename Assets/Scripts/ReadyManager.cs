using System;
using System.Collections.Generic;
using System.Linq;
using Events;
using Fusion;
using JetBrains.Annotations;
using UnityEngine;

public class ReadyManager : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnMatchStartedChanged))]
    private NetworkBool MatchStarted { get; set; }

    private readonly Dictionary<string, string> _gameScenes = new()
    {
        { "Basic", "Game_Scene_1" },
        { "Plus", "Game_Scene_2"},
        { "Chokepoint", "Game_Scene_3"},
    };

    public override void Spawned()
    {
        NetworkManager.Instance.ReadyManagerInstance = this;
    }
    
    // assignment 3
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (NetworkManager.Instance && NetworkManager.Instance.ReadyManagerInstance == this)
            NetworkManager.Instance.ReadyManagerInstance = null;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void KickPlayerRpc(PlayerRef playerToKick)
    {
        if (Runner.LocalPlayer == playerToKick)
        {
            Debug.Log("I was kicked, leaving room...");
            _ = NetworkManager.Instance.LeaveRoom(NetworkManager.Instance.CurrentLobbyId);
        }
    }

    public void StartMatch(string modeName, string mapName)
    {
        if (!HasStateAuthority) return;
        MatchStarted = true;
        
        Runner.SessionInfo.IsOpen    = false;
        // commented for assignment 3
        //Runner.SessionInfo.IsVisible = false;

        if (_gameScenes.TryGetValue(mapName, out var sceneName))
            Runner.LoadScene(sceneName);
        else
            Debug.LogError($"Map {mapName} not found!");
    }

    private void OnMatchStartedChanged()
    {
        if (MatchStarted)
            EventBus.Raise(new MatchStartedEvent());
    }
}
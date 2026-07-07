using System;
using System.Linq;
using Events;
using Fusion;
using UnityEngine;

public class ReadyManager : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnMatchStartedChanged))]
    private NetworkBool MatchStarted { get; set; }

    public override void Spawned()
    {
        NetworkManager.Instance.ReadyManagerInstance = this;
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
<<<<<<< HEAD
        Runner.SessionInfo.IsVisible = false;
        Runner.LoadScene("Game_Scene_1");
=======
        // commented for assignment 3
        //Runner.SessionInfo.IsVisible = false;
        Runner.LoadScene("Game_Scene");
>>>>>>> artur/UIFixes
    }

    private void OnMatchStartedChanged()
    {
        if (MatchStarted)
            EventBus.Raise(new MatchStartedEvent());
    }
}

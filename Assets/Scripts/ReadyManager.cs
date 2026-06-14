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

    public void StartMatch()
    {
        if (!HasStateAuthority) return;
        MatchStarted = true;
        
        Runner.SessionInfo.IsOpen    = false;
        Runner.SessionInfo.IsVisible = false;
        Runner.LoadScene("Game_Scene");
    }

    private void OnMatchStartedChanged()
    {
        if (MatchStarted)
            EventBus.Raise(new MatchStartedEvent());
    }
}

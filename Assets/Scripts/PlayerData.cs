using System.Net;
using Events;
using Fusion;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnDisplayNameChanged))]
    public NetworkString<_32> DisplayName { get; private set; }

    [Networked, OnChangedRender(nameof(OnReadyStatusChanged))]
    public NetworkBool IsReady { get; set; }
    
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            DisplayName = PlayerPrefs.GetString("PlayerName", $"Player_{Random.Range(1000, 9999)}");
        }
        
        NetworkManager.Instance.RegisterPlayer(Object.InputAuthority, this);
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        NetworkManager.Instance.UnregisterPlayer(Object.InputAuthority);
    }

    private void OnDisplayNameChanged() => 
        EventBus.Raise(new PlayerDataChangedEvent { PlayerRef = Object.InputAuthority });

    private void OnReadyStatusChanged()
    {
        EventBus.Raise(new PlayerDataChangedEvent { PlayerRef = Object.InputAuthority });
    }
}
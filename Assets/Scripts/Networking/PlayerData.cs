using Events;
using Fusion;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnDisplayNameChanged))]
    public NetworkString<_32> DisplayName { get; set; }

    [Networked, OnChangedRender(nameof(OnReadyStatusChanged))]
    public NetworkBool IsReady { get; set; }
    
    [Networked, OnChangedRender(nameof(OnCharacterChanged))]
    public int CharacterId { get; set; } = -1;
    
    [Networked, OnChangedRender(nameof(OnCharacterChanged))]
    public NetworkBool HasSelectedCharacter { get; set; }
    
    [Networked] public float ColorR { get; set; }
    [Networked] public float ColorG { get; set; }
    [Networked] public float ColorB { get; set; }
    
    public Color CharacterColor => new(ColorR, ColorG, ColorB);
    
    public override void Spawned()
    {
        if (HasStateAuthority && string.IsNullOrEmpty(DisplayName.Value))
        {
            DisplayName = $"Player_{Object.InputAuthority.PlayerId}";
            CharacterId = -1;
            HasSelectedCharacter = false;
        }

        if (NetworkManager.Instance)
            NetworkManager.Instance.RegisterPlayer(Object.InputAuthority, this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (NetworkManager.Instance)
            NetworkManager.Instance.UnregisterPlayer(Object.InputAuthority);
    }

    // Server-side seeding of the display name from the connection token.
    public void ServerInitialize(string displayName)
    {
        if (!HasStateAuthority) return;
        DisplayName = string.IsNullOrEmpty(displayName)
            ? $"Player_{Object.InputAuthority.PlayerId}"
            : displayName;
        CharacterId = -1;
        HasSelectedCharacter = false;
    }

    public void ApplyConfirmedName(string confirmedName)
    {
        if (HasStateAuthority) DisplayName = confirmedName;
        else Rpc_SetName(confirmedName);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetName(NetworkString<_32> confirmedName) => DisplayName = confirmedName;

    // Ready toggle: client has input authority, so it asks the server (state authority).
    public void RequestSetReady(bool ready)
    {
        if (HasStateAuthority) IsReady = ready;
        else Rpc_SetReady(ready);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetReady(NetworkBool ready)
    {
        IsReady = ready;
        
        if (HasStateAuthority)
        {
            var announcer = Runner ? Runner.GetComponent<RunnerChatAnnouncer>() : null;
            announcer?.AnnounceReady(DisplayName.ToString(), ready);
        }
    }
    
    public void ApplyCharacterSelection(int characterId, Color color)
    {
        if (!HasStateAuthority)
            return;
        CharacterId = characterId;
        ColorR = color.r;
        ColorG = color.g;
        ColorB = color.b;
        HasSelectedCharacter = true;
    }

    private void OnDisplayNameChanged() =>
        EventBus.Raise(new PlayerDataChangedEvent { PlayerRef = Object.InputAuthority });

    private void OnReadyStatusChanged() =>
        EventBus.Raise(new PlayerDataChangedEvent { PlayerRef = Object.InputAuthority });

    private void OnCharacterChanged() =>
        EventBus.Raise(new PlayerDataChangedEvent { PlayerRef = Object.InputAuthority });
}

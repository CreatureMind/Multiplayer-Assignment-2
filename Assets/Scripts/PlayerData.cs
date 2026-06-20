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
        if (HasStateAuthority)
        {
            // Restore the chosen name after a game-scene respawn; fall back to
            // the default only before the player has confirmed a name.
            var confirmedName = NetworkManager.Instance ? NetworkManager.Instance.LocalConfirmedName : null;
            DisplayName = string.IsNullOrEmpty(confirmedName)
                ? $"Player_{Object.InputAuthority.PlayerId}"
                : confirmedName;
            CharacterId = -1;
            HasSelectedCharacter = false;
        }

        NetworkManager.Instance.RegisterPlayer(Object.InputAuthority, this);
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        NetworkManager.Instance.UnregisterPlayer(Object.InputAuthority);
    }
    
    public void ApplyConfirmedName(string confirmedName)
    {
        if (!HasStateAuthority)
            return;
        DisplayName = confirmedName;
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
using Events;
using Fusion;

public class CharacterSelectionManager : NetworkBehaviour
{
    [Networked] private NetworkArray<PlayerRef> CharacterOwners { get; } = MakeInitializer(new PlayerRef[10]);
    
    public static CharacterSelectionManager Instance { get; private set; }

    public override void Spawned()
    {
        Instance = this;

        EventBus.Raise(new CharacterSelectionManagerReadyEvent());
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsCharacterClaimed(int characterId)
    {
        if (characterId is < 0 or >= 10)
            return true;
        return CharacterOwners[characterId] != PlayerRef.None;
    }

    public PlayerRef GetCharacterOwner(int characterId)
        => characterId is < 0 or >= 10 ? PlayerRef.None : CharacterOwners[characterId];

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RequestCharacterRpc(int characterId, PlayerRef requester)
    {
        if (!HasStateAuthority)
            return;

        if (characterId is < 0 or >= 10)
        {
            DenyCharacterRpc(characterId, requester);
            return;
        }
        
        var currentOwner = CharacterOwners[characterId];

        if (currentOwner != PlayerRef.None && currentOwner != requester)
        {
            DenyCharacterRpc(characterId, requester);
            return;
        }

        ReleaseAllCharactersForPlayer(requester);
        CharacterOwners.Set(characterId, requester);
        ApplySelectionToPlayerDataRpc(characterId, requester);
        ConfirmCharacterRpc(characterId, requester);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ConfirmCharacterRpc(int characterId, PlayerRef owner)
    {
        EventBus.Raise(new CharacterClaimedEvent { CharacterId = characterId, ClaimedBy = owner });
        
        if (Runner.LocalPlayer == owner)
            EventBus.Raise(new CharacterSelectionConfirmedEvent { CharacterId = characterId });
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void DenyCharacterRpc(int characterId, PlayerRef requester)
    {
        if (Runner.LocalPlayer != requester)
            return;
        EventBus.Raise(new CharacterSelectionDeniedEvent { CharacterId = characterId });
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ApplySelectionToPlayerDataRpc(int characterId, PlayerRef target)
    {
        if (Runner.LocalPlayer != target)
            return;

        var playerData = NetworkManager.Instance.GetLocalPlayerData();
        if (!playerData)
            return;
        
        var registry = NetworkManager.Instance.CharacterRegistry;
        if (registry == null)
            return;

        var def = registry.GetById(characterId);
        if (def == null)
            return;

        playerData.ApplyCharacterSelection(characterId, def.CharacterColor);

        // TODO: Trigger player spawn here once the spawning exists
    }
    
    private void ReleaseAllCharactersForPlayer(PlayerRef player)
    {
        for (var i = 0; i < 10; i++)
        {
            if (CharacterOwners[i] != player)
                continue;

            CharacterOwners.Set(i, PlayerRef.None);
            // Notify all clients that this slot is now free.
            ReleaseCharacterRpc(i);
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ReleaseCharacterRpc(int characterId)
    {
        EventBus.Raise(new CharacterReleasedEvent { CharacterId = characterId });
    }
    
    public void OnPlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;
        ReleaseAllCharactersForPlayer(player);
    }
}
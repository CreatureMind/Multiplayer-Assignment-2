using Fusion;
using UnityEngine;

public class GameSessionManager : NetworkBehaviour, IStateAuthorityChanged
{
    public static GameSessionManager Instance { get; private set; }

    [Networked, OnChangedRender(nameof(OnGameEndedChanged))]
    private NetworkBool GameEnded { get; set; }

    public override void Spawned()
    {
        Instance = this;
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void EndGameSession()
    {
        if (!HasStateAuthority) return;

        GameEnded = true;
    }

    private void OnGameEndedChanged()
    {
        if (!GameEnded) return;

        var isMasterClient = Runner.IsSharedModeMasterClient;

        if (GameUIManager.Instance)
        {
            GameUIManager.Instance.OnGameEnded(isMasterClient);
        }
    }
    
    public void StateAuthorityChanged()
    {
        if (GameUIManager.Instance)
            GameUIManager.Instance.NotifyMasterClientMightHaveChanged();
    }
}

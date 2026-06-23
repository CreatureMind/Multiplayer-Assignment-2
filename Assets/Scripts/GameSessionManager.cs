using Fusion;
using UnityEngine;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    public override void Spawned()
    {
        Instance = this;
    }

    public void EndGameSession()
    {
        if (!HasStateAuthority) return;
        
        RPC_NotifyGameEnded();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyGameEnded()
    {
        var isMasterClient = Runner.IsSharedModeMasterClient;
        
        if (GameUIManager.Instance)
        {
            GameUIManager.Instance.OnGameEnded(isMasterClient);
        }
    }
}

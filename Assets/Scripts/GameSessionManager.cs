using Fusion;
using UnityEngine;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Networked, OnChangedRender(nameof(OnGameEndedChanged))]
    private NetworkBool GameEnded { get; set; }

    public override void Spawned()
    {
        Instance = this;
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
}

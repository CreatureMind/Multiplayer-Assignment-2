using System;
using Fusion;
using UnityEngine;

public class InGameModel
{
    public event Action OnMasterClientChanged;
    public event Action<bool> OnGameEndedByMaster;

    private const string LOBBY_SCENE = "Lobby_Scene";

    public bool IsMasterClient()
    {
        if (!NetworkManager.Instance) return false;
        return NetworkManager.Instance.CanStartGame();
    }

    public void ReturnToLobby()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(LOBBY_SCENE);
    }

    public void CheckMasterClientStatus()
    {
        OnMasterClientChanged?.Invoke();
    }

    public void NotifyGameEnded(bool isMasterClient)
    {
        OnGameEndedByMaster?.Invoke(isMasterClient);
    }
}

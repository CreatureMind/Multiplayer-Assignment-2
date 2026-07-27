using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameUIModel
{
    public event Action OnMasterClientChanged;
    public event Action<bool> OnGameEndedByMaster;

    private const string LOBBY_SCENE = "Lobby_Scene";

    public bool IsMasterClient()
    {
        if (!NetworkManager.Instance) return false;
        return NetworkManager.Instance.CanStartGame();
    }

    public void ReturnToLobby(float flushDelay = 0f)
    {
        Debug.Log("[InGameUI] Returning to lobby.");
        if (NetworkManager.Instance)
        {
            Debug.Log("[InGameUI] Returning to lobby.");
            _ = NetworkManager.Instance.ReturnToLobbyAsync(flushDelay);
        }
        else
        {
            Debug.Log("[InGameUI] Returning to lobby (no network manager).");
            SceneManager.LoadScene(LOBBY_SCENE);
        }
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

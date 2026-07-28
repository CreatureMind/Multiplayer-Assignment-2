using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

public class InGameUIModel
{
    public event Action OnGameEnded;

    public void ReturnToLobby(float flushDelay = 0f)
    {
        Debug.Log("[InGameUI] Returning to lobby...");
        
        if (NetworkManager.Instance)
        {
            Debug.Log("[InGameUI] Returning to lobby through network manager.");
            _ = NetworkManager.Instance.ReturnToLobbyAsync(flushDelay);
        }
        else
        {
            Debug.Log("[InGameUI] Returning to lobby (no network manager).");
            SceneManager.LoadScene((int)SceneDefs.MENU, LoadSceneMode.Single);
        }
    }

    public void NotifyGameEnded()
    {
        OnGameEnded?.Invoke();
    }
}

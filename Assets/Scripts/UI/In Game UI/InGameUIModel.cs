using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

public class InGameUIModel
{
    public event Action OnGameEnded;
    
    private NetworkManager _networkManager;
    
    public InGameUIModel(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public void ReturnToLobby(float flushDelay = 0f)
    {
        Debug.Log("[InGameUI] Returning to lobby...");
        
        if (_networkManager)
        {
            Debug.Log("[InGameUI] Returning to lobby through network manager.");
            _ = _networkManager.ReturnToLobbyAsync(flushDelay);
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

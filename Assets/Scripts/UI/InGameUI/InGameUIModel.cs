using System;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

public class InGameUIModel
{
    public event Action<string> OnGameEnded;
    
    private NetworkManager _networkManager;
    
    public InGameUIModel(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public void ReturnToLobby(float flushDelay = 0f)
    {
        Debug.Log("[InGameUIModel] Returning to lobby...");
        
        if (_networkManager)
        {
            Debug.Log("[InGameUIModel] Returning to lobby through network manager.");
            _ = _networkManager.ReturnToLobbyAsync(flushDelay);
        }
        else
        {
            Debug.Log("[InGameUIModel] Returning to lobby (no network manager).");
            SceneManager.LoadSceneAsync((int)SceneDefs.MENU, LoadSceneMode.Single);
        }
    }

    public void NotifyGameEnded(PlayerRef player)
    {
        var isLocalPlayer = _networkManager.IsLocalPlayer(player);
        var playersData = _networkManager.GetPlayerDataMap();

        var winingPlayerName = playersData.Where(playerData => playerData.Key == player)
            .Select(playerData => playerData.Value.DisplayName.ToString())
            .FirstOrDefault();

        var wonText = isLocalPlayer ? "Congratz, You won! :D" : $"You lost! :'( {winingPlayerName} Won!";
        
        OnGameEnded?.Invoke(wonText);
    }
}

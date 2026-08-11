using System;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

public class InGameUIModel
{
    private const string WON_TEXT = "Congratz, You won! :D";
    private const string LOST_TEXT = "You lost! :'(";
    public event Action<string> OnGameEnded;
    
    private NetworkManager _networkManager;
    
    public InGameUIModel(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public void ReturnToLobby(float flushDelay = 0f)
    {
        if (_networkManager)
        {
            _ = _networkManager.ReturnToLobbyAsync(flushDelay);
        }
        else
        {
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

        var wonText = isLocalPlayer ? WON_TEXT : LOST_TEXT + $"{winingPlayerName} Won!";
        
        OnGameEnded?.Invoke(wonText);
    }
}

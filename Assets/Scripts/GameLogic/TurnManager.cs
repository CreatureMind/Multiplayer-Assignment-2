using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class TurnManager: NetworkBehaviour
{
    private List<ClientManager> _clientManagers = new List<ClientManager>();
    private int _currentTurnIndex;

    [Networked, OnChangedRender(nameof(CurrentTurnIndexChanged))]private int CurrentTurnIndex
    {
        get => _currentTurnIndex;
        set => _currentTurnIndex = value % _clientManagers.Count;
    }

    private void CurrentTurnIndexChanged()
    {
        //all classes get notified in the change of the turn
        // this should promped the client to update the UI and allow the player to make a move
        Debug.Log($"Turn changed to player: {_clientManagers[CurrentTurnIndex].PlayerId}");
        
    }

    public void InstantiateTurnManager(List<ClientManager> clientManagers)
    {
        _clientManagers = clientManagers;
        RandomizeTurnOrder();
    }

    private void RandomizeTurnOrder()
    {
        CurrentTurnIndex = Random.Range(0, _clientManagers.Count);
    }
    
    
}

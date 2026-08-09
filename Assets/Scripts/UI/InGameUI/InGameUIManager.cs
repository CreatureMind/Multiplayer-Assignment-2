using System;
using Events;
using Fusion;
using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
    [SerializeField] private InGameUIView inGameView;
    
    public event Action<PlayerRef> OnMatchEnded;

    private InGameUIModel     _model;
    private InGameUIPresenter _presenter;

    private NetworkManager _networkManager;
    public NetworkManager  NetworkManager
    {
        get => _networkManager;
        set => _networkManager = value;
    }

    private void Start()
    {
        InitializeMvp();
    }

    private void InitializeMvp()
    {
        if (!inGameView)
        {
            Debug.LogError("[InGameUIManager] InGameUIView component not found on InGameUIManager!");
            return;
        }

        _model = new InGameUIModel(_networkManager);
        _presenter = new InGameUIPresenter(_model, inGameView);

        SubscribeToGlobalEvents();
    }

    private void SubscribeToGlobalEvents()
    {
        OnMatchEnded += HandleGameEnded;
    }
    
    private void UnsubscribeGlobalEvents()
    {
        OnMatchEnded -= HandleGameEnded;
    }

    private void HandleGameEnded(PlayerRef player)
    {
        _model?.NotifyGameEnded(player);
    }

    private void OnDestroy()
    {
        UnsubscribeGlobalEvents();
        
        _presenter?.UnsubscribeFromEvents();
    }
}

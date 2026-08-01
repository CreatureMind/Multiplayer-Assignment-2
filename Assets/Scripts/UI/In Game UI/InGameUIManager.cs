using System;
using Events;
using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
    [SerializeField] private InGameUIView inGameView;

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
    }

    public void OnGameEnded()
    {
        _model?.NotifyGameEnded();
    }

    private void OnDestroy()
    {
        _presenter?.UnsubscribeFromEvents();
    }
}

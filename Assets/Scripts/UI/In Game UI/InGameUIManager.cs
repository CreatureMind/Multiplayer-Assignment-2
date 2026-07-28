using System;
using Events;
using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
    [SerializeField] private InGameUIView inGameView;

    private InGameUIModel _model;
    private InGameUIPresenter _presenter;

    private NetworkManager _networkManager;
    public NetworkManager NetworkManager
    {
        get => _networkManager;
        set
        {
            _networkManager = value;
        }
    }

    private void Start()
    {
        InitializeMVP();
    }

    private void InitializeMVP()
    {
        if (inGameView == null)
        {
            Debug.LogError("[GameUIManager] InGameUIView component not found on InGameUIManager!");
            return;
        }

        _model = new InGameUIModel(_networkManager);
        _presenter = new InGameUIPresenter(_model, inGameView);
    }

    public void OnGameEnded()
    {
        if (_model != null)
        {
            _model.NotifyGameEnded();
        }
    }

    private void OnDestroy()
    {
        if (_presenter != null)
        {
            _presenter.UnsubscribeFromEvents();
        }
    }
}

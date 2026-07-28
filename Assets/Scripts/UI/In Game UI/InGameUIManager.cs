using System;
using Events;
using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
    [SerializeField] private InGameUIView inGameView;
    
    public static InGameUIManager Instance { get; private set; }

    private InGameUIModel _model;
    private InGameUIPresenter _presenter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

        _model = new InGameUIModel();
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

        if (Instance == this)
        {
            Instance = null;
        }
    }
}

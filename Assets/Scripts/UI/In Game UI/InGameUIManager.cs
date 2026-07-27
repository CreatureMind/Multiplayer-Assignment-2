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

        InitializeMVP();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
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

        _presenter.Initialize();
    }

    private void OnPlayerListChanged(PlayerListChangedEvent e)
    {
        if (_presenter != null)
        {
            _presenter.CheckForMasterClientChange();
        }
    }

    public void OnGameEnded(bool isMasterClient)
    {
        if (_model != null)
        {
            _model.NotifyGameEnded(isMasterClient);
        }
    }
    
    public void NotifyMasterClientMightHaveChanged()
    {
        _presenter?.CheckForMasterClientChange();
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

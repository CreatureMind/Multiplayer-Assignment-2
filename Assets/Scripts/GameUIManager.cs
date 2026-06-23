using Events;
using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] private InGameView inGameView;
    
    public static GameUIManager Instance { get; private set; }

    private InGameModel _model;
    private InGamePresenter _presenter;

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
            Debug.LogError("InGameView component not found on GameUIManager!");
            return;
        }

        _model = new InGameModel();
        _presenter = new InGamePresenter(_model, inGameView);

        _presenter.Initialize();
    }

    private void OnPlayerListChanged(PlayerListChangedEvent e)
    {
        // Check for master client changes when player list changes
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

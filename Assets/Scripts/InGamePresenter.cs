using UnityEngine;

public class InGamePresenter
{
    private readonly InGameModel _model;
    private readonly InGameView _view;

    public InGamePresenter(InGameModel model, InGameView view)
    {
        _model = model;
        _view = view;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _view.OnEndGameButtonClicked += HandleEndGameButtonClicked;
        _view.OnLeaveButtonClicked += HandleLeaveButtonClicked;
        _view.OnReturnToLobbyClicked += HandleReturnToLobbyClicked;

        _model.OnMasterClientChanged += UpdateButtonVisibility;
        _model.OnGameEndedByMaster += HandleGameEnded;
    }

    public void UnsubscribeFromEvents()
    {
        _view.OnEndGameButtonClicked -= HandleEndGameButtonClicked;
        _view.OnLeaveButtonClicked -= HandleLeaveButtonClicked;
        _view.OnReturnToLobbyClicked -= HandleReturnToLobbyClicked;

        _model.OnMasterClientChanged -= UpdateButtonVisibility;
        _model.OnGameEndedByMaster -= HandleGameEnded;
    }

    public void Initialize()
    {
        UpdateButtonVisibility();
    }

    private void HandleEndGameButtonClicked()
    {
        if (!_model.IsMasterClient()) return;

        if (GameSessionManager.Instance)
        {
            GameSessionManager.Instance.EndGameSession();
        }
    }

    private void HandleLeaveButtonClicked()
    {
        _model.ReturnToLobby();
    }

    private void HandleReturnToLobbyClicked()
    {
        _model.ReturnToLobby();
    }

    private void UpdateButtonVisibility()
    {
        var isMasterClient = _model.IsMasterClient();
        _view.SetEndGameButtonVisible(isMasterClient);
        _view.SetLeaveButtonVisible(true); // All players can see the leave button
    }

    private void HandleGameEnded(bool isMasterClient)
    {
        if (isMasterClient)
        {
            // Master client immediately returns to lobby
            _model.ReturnToLobby();
        }
        else
        {
            // Other players see the popup
            _view.ShowEndGamePopup();
        }
    }

    public void CheckForMasterClientChange()
    {
        _model.CheckMasterClientStatus();
    }
}

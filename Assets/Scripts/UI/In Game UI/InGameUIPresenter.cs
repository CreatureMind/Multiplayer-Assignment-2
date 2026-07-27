using UnityEngine;

public class InGameUIPresenter
{
    private readonly InGameUIModel _model;
    private readonly InGameUIView _view;

    public InGameUIPresenter(InGameUIModel model, InGameUIView view)
    {
        _model = model;
        _view = view;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        //_view.OnEndGameButtonClicked += HandleEndGameButtonClicked;
        _view.OnLeaveButtonClicked += HandleLeaveButtonClicked;
        _view.OnReturnToLobbyClicked += HandleReturnToLobbyClicked;

        _model.OnMasterClientChanged += UpdateButtonVisibility;
        _model.OnGameEndedByMaster += HandleGameEnded;
    }

    public void UnsubscribeFromEvents()
    {
        //_view.OnEndGameButtonClicked -= HandleEndGameButtonClicked;
        _view.OnLeaveButtonClicked -= HandleLeaveButtonClicked;
        _view.OnReturnToLobbyClicked -= HandleReturnToLobbyClicked;

        _model.OnMasterClientChanged -= UpdateButtonVisibility;
        _model.OnGameEndedByMaster -= HandleGameEnded;
    }

    public void Initialize()
    {
        UpdateButtonVisibility();
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
        _view.SetLeaveButtonVisible(true);
    }

    private void HandleGameEnded(bool isMasterClient)
    {
        if (isMasterClient)
            _model.ReturnToLobby(0.3f); // delay so the end signal reaches others first
        else
            _view.ShowEndGamePopup();
    }

    public void CheckForMasterClientChange()
    {
        _model.CheckMasterClientStatus();
    }
}

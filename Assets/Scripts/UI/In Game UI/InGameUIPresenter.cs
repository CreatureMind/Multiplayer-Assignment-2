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
        _view.OnLeaveButtonClicked += ReturnToLobby;
        _view.OnReturnButtonClicked += ReturnToLobby;

        _model.OnGameEnded += HandleGameEnded;
    }

    public void UnsubscribeFromEvents()
    {
        _view.OnLeaveButtonClicked -= ReturnToLobby;
        _view.OnReturnButtonClicked -= ReturnToLobby;
        
        _model.OnGameEnded -= HandleGameEnded;
    }

    private void ReturnToLobby()
    {
        Debug.Log("<color=yellow>[UI PRESENTER] Catching leave event, calling model...</color>");
        _model.ReturnToLobby();
    }

    private void HandleGameEnded()
    {
        _view.ShowEndGamePopup();
    }
}

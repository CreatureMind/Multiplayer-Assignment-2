using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomCreationUIPresenter
    {
        private readonly RoomCreationUIModel _model;
        private readonly RoomCreationUIView _view;
        private string _currentLobbyId;

        public RoomCreationUIPresenter(RoomCreationUIModel model, RoomCreationUIView view, string currentLobbyId)
        {
            _model = model;
            _view = view;
            _currentLobbyId = currentLobbyId;

            SubscribeToEvents();
        }

        public void SetLobbyId(string lobbyId)
        {
            _currentLobbyId = lobbyId;
        }

        private void SubscribeToEvents()
        {
            _view.OnCreateRequested += HandleCreateRequested;
            _view.OnBackRequested   += HandleBackRequested;
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnCreateRequested -= HandleCreateRequested;
            _view.OnBackRequested   -= HandleBackRequested;
        }

        private void HandleCreateRequested(RoomCreationFormData formData)
        {
            if (string.IsNullOrEmpty(formData.RoomName))
            {
                Debug.LogWarning("[RoomCreationPresenter] Room name cannot be empty!");
                return;
            }

            _view.SetCreateButtonEnabled(false);
            _model.CreateRoom(formData, _currentLobbyId);
            _view.Hide();
        }

        private void HandleBackRequested()
        {
            _view.Hide();
        }
    }
}
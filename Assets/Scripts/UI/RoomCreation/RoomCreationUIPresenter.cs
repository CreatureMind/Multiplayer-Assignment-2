using System;
using Events;
using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomCreationUIPresenter
    {
        private readonly RoomCreationUIModel _model;
        private readonly RoomCreationUIView _view;
        private string _currentLobbyId;
        private readonly Action _onRoomCreatedRequested;

        public RoomCreationUIPresenter(RoomCreationUIModel model, RoomCreationUIView view, string currentLobbyId, Action onRoomCreatedRequested = null)
        {
            _model = model;
            _view = view;
            _currentLobbyId = currentLobbyId;
            _onRoomCreatedRequested = onRoomCreatedRequested;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnCreateRequested += HandleCreateRequested;
            _view.OnBackRequested   += HandleBackRequested;
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<OpenRoomCreationOverlayEvent>(HandleOpenRoomCreationRequested);
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnCreateRequested -= HandleCreateRequested;
            _view.OnBackRequested   -= HandleBackRequested;
            
            EventBus.Unsubscribe<OpenRoomCreationOverlayEvent>(HandleOpenRoomCreationRequested);
        }

        private void HandleOpenRoomCreationRequested(OpenRoomCreationOverlayEvent e) => _view.Show();

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
            _onRoomCreatedRequested?.Invoke();

            _view.ResetView();
        }

        private void HandleBackRequested()
        {
            _view.Hide();
        }
    }
}
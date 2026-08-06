using System;
using Events;
using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomJoinUIPresenter
    {
        private readonly RoomJoinUIModel _model;
        private readonly RoomJoinUIView _view;
        private readonly Action _onJoinRequested;

        public RoomJoinUIPresenter(RoomJoinUIModel model, RoomJoinUIView view, Action onJoinRequested = null)
        {
            _model = model;
            _view = view;
            _onJoinRequested = onJoinRequested;

            SubscribeToViewEvents();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnJoinRequested += HandleJoinRequested;
            _view.OnBackRequested += HandleBackRequested;
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnJoinRequested -= HandleJoinRequested;
            _view.OnBackRequested -= HandleBackRequested;
        }
        
        private void HandleJoinRequested()
        {
            var trimmed = _view.GetInputValue().Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                _view.ShowError("Please enter a Room Code.");
                return;
            }

            if (trimmed.Length < 8)
            {
                _view.ShowError("Room Code must be 8 characters.");
                return;
            }

            _view.SetJoinButtonEnabled(false);
            _model.JoinRoom(trimmed);
            _view.Hide();
            _onJoinRequested?.Invoke();

            _view.ResetView();
        }

        private void HandleBackRequested()
        {
            _view.Hide();
        }
    }
}
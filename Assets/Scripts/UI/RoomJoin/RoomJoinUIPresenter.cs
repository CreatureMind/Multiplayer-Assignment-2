using System;
using System.Collections.Generic;
using System.Linq;
using Events;
using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomJoinUIPresenter
    {
        private readonly RoomJoinUIModel _model;
        private readonly RoomJoinUIView _view;
        private readonly Action<string, string, string, bool, string> _onJoinRequested;

        public RoomJoinUIPresenter(RoomJoinUIModel model, RoomJoinUIView view, Action<string, string, string, bool, string> onJoinRequested = null)
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
        
        private void HandleJoinRequested(List<RoomInfo> rooms)
        {
            var trimmed = _view.GetInputValue().Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.Length != 8)
            {
                _view.ShowError("Room Code must be 8 characters.");
                return;
            }
            
            if (rooms == null || rooms.Count == 0)
            {
                _view.ShowError("No active rooms found.");
                return;
            }
            
            var privateRoom = rooms.FirstOrDefault(r => r.RoomCode == trimmed);
            
            if (string.IsNullOrEmpty(privateRoom.RoomCode))
            {
                _view.ShowError("Room not found. Check the code and try again.");
                return;
            }
            
            _view.SetJoinButtonEnabled(false);
            _model.JoinRoom(trimmed);
            _view.Hide();
            
            _onJoinRequested?.Invoke(
                privateRoom.DisplayName.Value,
                privateRoom.ModeName,
                privateRoom.MapName,
                privateRoom.IsPublic,
                privateRoom.RoomCode);

            _view.ResetView();
        }

        private void HandleBackRequested()
        {
            _view.Hide();
        }
    }
}
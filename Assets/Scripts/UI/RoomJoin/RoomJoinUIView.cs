using System;
using System.Collections.Generic;
using UI.Common;
using UI.RoomsList;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomJoinUIView : BaseOverlayView
    {
        public event Action<List<RoomInfo>> OnJoinRequested;
        public event Action OnBackRequested;

        private TextField _roomCodeField;
        private Label     _errorLabel;
        private Button    _joinButton;
        private Button    _backButton;

        protected override void OnInitializeUI()
        {
            if (Root == null) return;
            
            _roomCodeField = Root.Q<TextField>(UI_Join_Room_View.room_code_field);
            _errorLabel    = Root.Q<Label>    (UI_Join_Room_View.error_label);
            _joinButton    = Root.Q<Button>   (UI_Join_Room_View.join_button);
            _backButton    = Root.Q<Button>   (UI_Join_Room_View.back_button);
            
            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_joinButton != null)
            {
                _joinButton.clicked += () =>
                {
                    if (RoomsListUIModel.CachedRoomData == null)
                    {
                        Debug.LogError("[RoomJoinUIView] CachedRoomData is null!.");
                        return;
                    }
                    var rooms = RoomsListUIModel.CachedRoomData.Value.Rooms;

                    OnJoinRequested?.Invoke(rooms);
                };
            }
            else
            {
                Debug.LogError("[RoomJoinUIView] Could not find Button named 'create-button' in Room_Creation_View.");
            }

            if (_backButton != null)
            {
                _backButton.clicked += () => OnBackRequested?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomJoinUIView] Could not find Button named 'back-button' in Room_Creation_View.");
            }
            
            Hide();
        }
        
        public string GetInputValue() => _roomCodeField != null ? _roomCodeField.value : string.Empty;
        
        public void ShowError(string message)
        {
            if (_errorLabel != null)
                _errorLabel.text = message;
        }

        public void SetJoinButtonEnabled(bool isEnabled) => _joinButton?.SetEnabled(isEnabled);

        public void ResetView()
        {
            SetJoinButtonEnabled(true);
            _roomCodeField.value = string.Empty;
            _errorLabel.text = string.Empty;
        }
        
        protected override void OnShow()
        {
            ResetView();
        }
    }
}
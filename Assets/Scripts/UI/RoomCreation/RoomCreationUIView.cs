using System;
using UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomCreationUIView : BaseOverlayView
    {
        [SerializeField] private int maxPlayersMinValue = 2;
        [SerializeField] private int maxPlayersMaxValue = 4;
        
        public event Action<RoomCreationFormData> OnCreateRequested;
        public event Action OnBackRequested;

        private TextField     _roomNameField;
        private SliderInt     _maxPlayersSlider;
        private DropdownField _modesDropdown;
        private DropdownField _mapsDropdown;
        private Toggle        _publicToggle;
        private Button        _createButton;
        private Button        _backButton;
        
        private bool _isVisible = true;

        protected override void OnInitializeUI()
        {
            if (Root == null) return;

            _roomNameField    = Root.Q<TextField>    (UI_Room_Creation_View.room_name);
            _maxPlayersSlider = Root.Q<SliderInt>    (UI_Room_Creation_View.max_players);
            _modesDropdown    = Root.Q<DropdownField>(UI_Room_Creation_View.modes_dropdown);
            _mapsDropdown     = Root.Q<DropdownField>(UI_Room_Creation_View.maps_dropdown);
            _publicToggle     = Root.Q<Toggle>       (UI_Room_Creation_View.public_toggle);
            _createButton     = Root.Q<Button>       (UI_Room_Creation_View.create_button);
            _backButton       = Root.Q<Button>       (UI_Room_Creation_View.back_button);

            _maxPlayersSlider.lowValue  = maxPlayersMinValue;
            _maxPlayersSlider.highValue = maxPlayersMaxValue;
            
            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_createButton != null)
            {
                _createButton.clicked += () =>
                {
                    var formData = new RoomCreationFormData
                    {
                        RoomName     = _roomNameField?.value,
                        MaxPlayers   = _maxPlayersSlider.value,
                        SelectedMode = _modesDropdown?.value,
                        SelectedMap  = _mapsDropdown?.value,
                        IsPublic     = _publicToggle.value
                    };
                    
                    OnCreateRequested?.Invoke(formData);
                };
            }
            else
            {
                Debug.LogError("[RoomCreationUIView] Could not find Button named 'create-button' in Room_Creation_View.");
            }

            if (_backButton != null)
            {
                _backButton.clicked += () => OnBackRequested?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomCreationUIView] Could not find Button named 'back-button' in Room_Creation_View.");
            }
            
            Hide();
        }

        public void SetCreateButtonEnabled(bool isEnabled) => _createButton?.SetEnabled(isEnabled);

        public void ResetView()
        {
            SetCreateButtonEnabled(true);
            _roomNameField.value = string.Empty;
            _maxPlayersSlider.value = maxPlayersMinValue;
            _mapsDropdown.index = 0;
            _modesDropdown.index = 0;
            _publicToggle.value = true;
        }
    }
}
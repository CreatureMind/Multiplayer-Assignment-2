using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomCreationUIView : MonoBehaviour
    {
        [SerializeField] private int maxPlayersMinValue = 2;
        [SerializeField] private int maxPlayersMaxValue = 4;
        
        public event Action<RoomCreationFormData> OnCreateRequested;
        public event Action OnBackRequested;

        private UIDocument    _document;
        private VisualElement _root;

        private TextField     _roomNameField;
        private SliderInt     _maxPlayersSlider;
        private DropdownField _modesDropdown;
        private DropdownField _mapsDropdown;
        private Toggle        _publicToggle;
        private Button        _createButton;
        private Button        _backButton;
        
        private bool _isVisible = true;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[RoomCreationUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _roomNameField    = _root.Q<TextField>    (UI_Room_Creation_View.room_name);
            _maxPlayersSlider = _root.Q<SliderInt>    (UI_Room_Creation_View.max_players);
            _modesDropdown    = _root.Q<DropdownField>(UI_Room_Creation_View.modes_dropdown);
            _mapsDropdown     = _root.Q<DropdownField>(UI_Room_Creation_View.maps_dropdown);
            _publicToggle     = _root.Q<Toggle>       (UI_Room_Creation_View.public_toggle);
            _createButton     = _root.Q<Button>       (UI_Room_Creation_View.create_button);
            _backButton       = _root.Q<Button>       (UI_Room_Creation_View.back_button);

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

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (_document) _document.sortingOrder = UIOverlaySorter.PushOverlay();

            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (_root != null) _root.style.display = DisplayStyle.None;

            UIOverlaySorter.PopOverlay();
        }

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
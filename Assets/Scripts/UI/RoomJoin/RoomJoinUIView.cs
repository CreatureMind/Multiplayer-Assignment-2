using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomJoinUIView : MonoBehaviour
    {
        
        public event Action OnJoinRequested;
        public event Action OnBackRequested;

        private UIDocument    _document;
        private VisualElement _root;

        private TextField _roomCodeField;
        private Label     _errorLabel;
        private Button    _joinButton;
        private Button    _backButton;
        
        private bool _isVisible = true;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[RoomJoinUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _roomCodeField = _root.Q<TextField>(UI_Join_Room_View.room_code_field);
            _errorLabel    = _root.Q<Label>    (UI_Join_Room_View.error_label);
            _joinButton    = _root.Q<Button>   (UI_Join_Room_View.join_button);
            _backButton    = _root.Q<Button>   (UI_Join_Room_View.back_button);
            
            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_joinButton != null)
            {
                _joinButton.clicked += () => OnJoinRequested?.Invoke();
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
            _roomCodeField.value = string.Empty;
            _errorLabel.text = string.Empty;
        }

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
    }
}
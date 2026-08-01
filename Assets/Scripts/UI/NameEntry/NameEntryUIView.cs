using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.NameEntry
{
    [RequireComponent(typeof(UIDocument))]
    public class NameEntryUIView : MonoBehaviour
    {
        public event Action OnConfirmClicked;
        public event Action OnRandomizeClicked;

        private UIDocument _document;
        private VisualElement _root;

        private TextField _nameField;
        private Button _confirmButton;
        private Button _randomButton;
        private Label _errorLabel;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[NameEntryUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _nameField = _root.Q<TextField>(UI_Name_Entry_View.player_name_field);
            _confirmButton = _root.Q<Button>(UI_Name_Entry_View.confirm_button);
            _randomButton = _root.Q<Button>(UI_Name_Entry_View.randomize_button);
            _errorLabel = _root.Q<Label>(UI_Name_Entry_View.error_label);

            if (_nameField == null || _confirmButton == null)
            {
                Debug.LogError("[NameEntryUIView] Required UI elements not found in Name_Entry_View.");
                return;
            }

            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_confirmButton != null)
            {
                _confirmButton.clicked += () => OnConfirmClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[NameEntryUIView] Could not find Button named 'confirm-button' in Name_Entry_View.");
            }

            if (_randomButton != null)
            {
                _randomButton.clicked += () => OnRandomizeClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[NameEntryUIView] Could not find Button named 'confirm-button' in Name_Entry_View.");
            }
            
            Hide();
        }

        public string GetInputValue() => _nameField != null ? _nameField.value : string.Empty;

        public void SetInputValue(string text)
        {
            if (_nameField != null)
                _nameField.value = text;
        }

        public void ShowError(string message)
        {
            if (_errorLabel != null)
                _errorLabel.text = message;
        }

        public void ClearError() => ShowError(string.Empty);

        public void Show()
        {
            if (_document) _document.sortingOrder = UIOverlaySorter.PushOverlay();
            
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            
            UIOverlaySorter.PopOverlay();
        }
    }
}
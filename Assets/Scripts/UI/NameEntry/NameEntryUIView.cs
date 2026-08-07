using System;
using UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.NameEntry
{
    [RequireComponent(typeof(UIDocument))]
    public class NameEntryUIView : BaseOverlayView
    {
        public event Action OnConfirmClicked;
        public event Action OnRandomizeClicked;

        private TextField _nameField;
        private Button    _confirmButton;
        private Button    _randomButton;
        private Label     _errorLabel;

        protected override void OnInitializeUI()
        {
            if (Root == null) return;

            _nameField     = Root.Q<TextField>(UI_Name_Entry_View.player_name_field);
            _confirmButton = Root.Q<Button>   (UI_Name_Entry_View.confirm_button);
            _randomButton  = Root.Q<Button>   (UI_Name_Entry_View.randomize_button);
            _errorLabel    = Root.Q<Label>    (UI_Name_Entry_View.error_label);

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
                Debug.LogError("[NameEntryUIView] Could not find Button named 'randomize-button' in Name_Entry_View.");
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
    }
}
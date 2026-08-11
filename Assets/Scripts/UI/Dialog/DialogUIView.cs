using System;
using Events;
using UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Dialog
{
    [RequireComponent(typeof(UIDocument))]
    public class DialogUIView : BaseOverlayView
    {
        public event Action OnPrimaryClicked;
        public event Action OnSecondaryClicked;
        public event Action OnTertiaryClicked;

        private Label _titleLabel;
        private Label _messageLabel;

        private Button _primaryBtn;
        private Button _secondaryBtn;
        private Button _tertiaryBtn;

        protected override void OnInitializeUI()
        {
            if (Root == null) return;
            
            _titleLabel   = Root.Q<Label>(UI_Dialog_View.header);
            _messageLabel = Root.Q<Label>(UI_Dialog_View.dialog_message);

            _primaryBtn   = Root.Q<Button>(UI_Dialog_View.primary_btn);
            _secondaryBtn = Root.Q<Button>(UI_Dialog_View.secondary_btn);
            _tertiaryBtn  = Root.Q<Button>(UI_Dialog_View.tertiary_btn);
            
            SetupCallbacks();
        }
        
        private void SetupCallbacks()
        {
            if (_primaryBtn != null)
            {
                _primaryBtn.clicked += () => OnPrimaryClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[DialogUIView] Could not find Button named 'primary-btn' in Dialog_View.");
            }

            if (_secondaryBtn != null)
            {
                _secondaryBtn.clicked += () => OnSecondaryClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[DialogUIView] Could not find Button named 'secondary-btn' in Dialog_View.");
            }

            if (_tertiaryBtn != null)
            {
                _tertiaryBtn.clicked += () => OnTertiaryClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[DialogUIView] Could not find Button named 'tertiary-btn' in Dialog_View.");
            }
            
            Hide();
        }

        public void SetContent(string title, string message, DialogType type)
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = title;
            }
            else
            {
                Debug.LogError("[DialogUIView] Could not find Label named 'header' in Dialog_View.");
            }

            if (_messageLabel != null)
            {
                _messageLabel.text = message;
            }
            else
            {
                Debug.LogError("[DialogUIView] Could not find Label named 'dialog-message' in Dialog_View.");
            }
        }

        public void SetButtons(string primaryText, string secondaryText, string tertiaryText)
        {
            ConfigureButton(_primaryBtn, primaryText);
            ConfigureButton(_secondaryBtn, secondaryText);
            ConfigureButton(_tertiaryBtn, tertiaryText);
        }

        private void ConfigureButton(Button btn, string text)
        {
            if (btn == null) return;

            if (!string.IsNullOrEmpty(text))
            {
                btn.text = text;
                btn.style.display = DisplayStyle.Flex;
            }
            else
            {
                btn.style.display = DisplayStyle.None;
            }
        }
    }
}
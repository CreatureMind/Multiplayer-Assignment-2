using System;
using Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Dialog
{
    [RequireComponent(typeof(UIDocument))]
    public class DialogUIView : MonoBehaviour
    {
        public event Action OnPrimaryClicked;
        public event Action OnSecondaryClicked;
        public event Action OnTertiaryClicked;

        private UIDocument    _document;
        private VisualElement _root;

        private Label _titleLabel;
        private Label _messageLabel;

        private Button _primaryBtn;
        private Button _secondaryBtn;
        private Button _tertiaryBtn;

        private bool _isVisible = true;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[DialogUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;
            
            _titleLabel   = _root.Q<Label>(UI_Dialog_View.header);
            _messageLabel = _root.Q<Label>(UI_Dialog_View.dialog_message);

            _primaryBtn   = _root.Q<Button>(UI_Dialog_View.primary_btn);
            _secondaryBtn = _root.Q<Button>(UI_Dialog_View.secondary_btn);
            _tertiaryBtn  = _root.Q<Button>(UI_Dialog_View.tertiary_btn);
            
            SetupCallbacks();
            
            Hide();
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
        }

        public void SetContent(string title, string message, DialogType type)
        {
            if (_titleLabel != null) _titleLabel.text = title;
            
            else Debug.LogError("[DialogUIView] Could not find Label named 'header' in Dialog_View.");
            
            if (_messageLabel != null) _messageLabel.text = message;
            
            else Debug.LogError("[DialogUIView] Could not find Label named 'dialog-message' in Dialog_View.");
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
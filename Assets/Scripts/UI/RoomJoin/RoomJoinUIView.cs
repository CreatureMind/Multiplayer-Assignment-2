using System;
using System.Collections.Generic;
using UI.RoomsList;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomJoinUIView : MonoBehaviour
    {
        public event Action<List<RoomInfo>> OnJoinRequested;
        public event Action OnBackRequested;

        private UIDocument    _document;
        private VisualElement _root;
        
        private VisualElement _tint;
        private VisualElement _container;

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
            
            _tint      = _root.Q<VisualElement>(UI_Join_Room_View.tint);
            _container = _root.Q<VisualElement>(UI_Join_Room_View.container);

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
                            
            if (_container != null)
            {
                _container?.RegisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            }
            else
            {
                Debug.LogError("[RoomJoinUIView] Could not find VisualElement named 'container' in Room_Creation_View.");
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
        
        private void SetPickingModeRecursive(VisualElement element, PickingMode mode)
        {
            if (element == null) return;
            element.pickingMode = mode;
            element.Query<VisualElement>().ForEach(child => child.pickingMode = mode);
        }

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (_document) _document.sortingOrder = UIOverlaySorter.PushOverlay();

            if (_root != null)
            {
                SetPickingModeRecursive(_tint, PickingMode.Position);
                _root.style.display = DisplayStyle.Flex;
                
                _root.schedule.Execute(() =>
                {
                    _tint?.RemoveFromClassList("overlay-tint--hidden");
                    _container?.RemoveFromClassList("overlay-container--hidden");
                });
            }
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (_root != null)
            {
                SetPickingModeRecursive(_tint, PickingMode.Ignore);
                
                _tint?.AddToClassList("overlay-tint--hidden");
                _container?.AddToClassList("overlay-container--hidden");
            }
        }
        
        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            if (!_isVisible && _root != null)
            {
                _root.style.display = DisplayStyle.None;
                UIOverlaySorter.PopOverlay();
            }
        }

        private void OnDestroy()
        {
            _container.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
        }
    }
}
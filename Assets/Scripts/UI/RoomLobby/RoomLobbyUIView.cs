using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomLobby
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomLobbyUIView : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset playerRowTemplate;

        public event Action            OnLeaveClicked;
        public event Action            OnReadyClicked;
        public event Action            OnStartClicked;
        public event Action<PlayerRef> OnKickClicked;

        private UIDocument    _document;
        private VisualElement _root;

        private Label      _headerLabel;
        private ScrollView _playerListScrollView;
        private Button     _leaveButton;
        private Button     _readyButton;
        private Button     _startButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[RoomLobbyUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _headerLabel          = _root.Q<Label>(UI_Room_View.header);
            _playerListScrollView = _root.Q<ScrollView>(UI_Room_View.players_scroll_view);
            _leaveButton          = _root.Q<Button>(UI_Room_View.leave_button);
            _readyButton          = _root.Q<Button>(UI_Room_View.ready_button);
            _startButton          = _root.Q<Button>(UI_Room_View.start_button);

            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_leaveButton != null)
            {
                _leaveButton.clicked += () => OnLeaveClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomLobbyUIView] Could not find Button named 'leave-btn' in Room_View.");
            }

            if (_readyButton != null)
            {
                _readyButton.clicked += () => OnReadyClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomLobbyUIView] Could not find Button named 'ready-btn' in Room_View.");
            }

            if (_startButton != null)
            {
                _startButton.clicked += () => OnStartClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomLobbyUIView] Could not find Button named 'start-btn' in Room_View.");
            }
        }

        public void SetHeader(string roomName, string modeName, string mapName)
        {
            if (_headerLabel != null)
                _headerLabel.text = $"{roomName} / {modeName} / {mapName}";
        }

        public void SetReadyButtonText(bool isReady)
        {
            if (_readyButton != null)
                _readyButton.text = isReady ? "Not Ready" : "Ready";
        }

        public void SetStartButtonState(bool isVisible, bool isEnabled)
        {
            if (_startButton == null) return;
            _startButton.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _startButton.SetEnabled(isEnabled);
        }

        public void SetLeaveButtonEnabled(bool isEnabled)
        {
            _leaveButton?.SetEnabled(isEnabled);
        }

        public void RenderPlayerList(IEnumerable<PlayerData> players, bool canKick, Func<PlayerRef, bool> isSelfFunc)
        {
            if (_playerListScrollView == null || playerRowTemplate == null) return;

            _playerListScrollView.Clear();

            foreach (var playerData in players)
            {
                if (playerData == null || !playerData.Object || !playerData.Object.IsValid) continue;

                var row = playerRowTemplate.CloneTree();

                var nameLabel = row.Q<Label>(UI_Player_Row_Template.player_name);
                if (nameLabel != null)
                    nameLabel.text = playerData.DisplayName.Value;

                var readyLabel = row.Q<Label>(UI_Player_Row_Template.ready_status);
                if (readyLabel != null)
                    readyLabel.text = playerData.IsReady ? "is Ready!" : "is Not Ready.";

                var kickBtn = row.Q<Button>(UI_Player_Row_Template.kick_button);
                if (kickBtn != null)
                {
                    var authority = playerData.Object.InputAuthority;
                    var isSelf = isSelfFunc(authority);

                    kickBtn.style.display = (canKick && !isSelf) ? DisplayStyle.Flex : DisplayStyle.None;
                    kickBtn.clicked += () => OnKickClicked?.Invoke(authority);
                }

                _playerListScrollView.Add(row);
            }
        }
        
        public void Show()
        {
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomsList
{
    [RequireComponent(typeof(UIDocument))]
    public class RoomsListUIView : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset roomRowTemplate;

        public event Action OnFilterChanged;
        public event Action OnLeaveClicked;
        public event Action OnJoinClicked;
        public event Action OnCreateRoomClicked;
        public event Action OnRefreshClicked;
        public event Action<RoomInfo, string, string, string> OnEnterRoomClicked;

        private UIDocument    _document;
        private VisualElement _root;

        private Label         _headerLabel;
        private DropdownField _roomsDropdown;
        private DropdownField _modesDropdown;
        private DropdownField _mapsDropdown;
        private ScrollView    _roomsScrollView;
        private Label         _onlineLabel;
        private Button        _leaveBtn;
        private Button        _joinBtn;
        private Button        _createBtn;
        private Button        _refreshBtn;

        public string SelectedRoomFilter => _roomsDropdown?.value ?? "All";
        public string SelectedModeFilter => _modesDropdown?.value ?? "All";
        public string SelectedMapFilter  => _mapsDropdown?.value ?? "All";

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[RoomsListUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _headerLabel     = _root.Q<Label>        (UI_Rooms_List_View.header);
            _roomsDropdown   = _root.Q<DropdownField>(UI_Rooms_List_View.rooms_dropdown);
            _modesDropdown   = _root.Q<DropdownField>(UI_Rooms_List_View.modes_dropdown);
            _mapsDropdown    = _root.Q<DropdownField>(UI_Rooms_List_View.maps_dropdown);
            _roomsScrollView = _root.Q<ScrollView>   (UI_Rooms_List_View.rooms_scroll_view);
            _onlineLabel     = _root.Q<Label>        (UI_Rooms_List_View.online_label);

            _leaveBtn   = _root.Q<Button>(UI_Rooms_List_View.leave_button);
            _joinBtn    = _root.Q<Button>(UI_Rooms_List_View.join_button);
            _createBtn  = _root.Q<Button>(UI_Rooms_List_View.create_button);
            _refreshBtn = _root.Q<Button>(UI_Rooms_List_View.refresh_button);
            

            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            _roomsDropdown?.RegisterValueChangedCallback(_ => OnFilterChanged?.Invoke());
            _modesDropdown?.RegisterValueChangedCallback(_ => OnFilterChanged?.Invoke());
            _mapsDropdown? .RegisterValueChangedCallback(_ => OnFilterChanged?.Invoke());

            if (_leaveBtn != null)
            {
                _leaveBtn.clicked += () => OnLeaveClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomsListUIView] Could not find Button named 'leave-btn' in Room_List_View.");
            }
            
            if (_joinBtn != null)
            {
                _joinBtn.clicked += () => OnJoinClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomsListUIView] Could not find Button named 'join-btn' in Room_List_View.");
            }

            if (_createBtn != null)
            {
                _createBtn.clicked += () => OnCreateRoomClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomsListUIView] Could not find Button named 'create-btn' in Room_List_View.");
            }

            if (_refreshBtn != null)
            {
                _refreshBtn.clicked += () => OnRefreshClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomsListUIView] Could not find Button named 'refresh-btn' in Room_List_View.");
            }
        }

        public void SetHeader(string text)
        {
            if (_headerLabel != null) _headerLabel.text = text;
        }

        public void SetOnlinePlayerCount(int totalPlayers)
        {
            if (_onlineLabel != null) _onlineLabel.text = $"Online Players: {totalPlayers}";
        }

        public void SetRefreshButtonEnabled(bool isEnabled)
        {
            _refreshBtn?.SetEnabled(isEnabled);
        }

        public void RenderRoomsList(List<RoomInfo> rooms)
        {
            if (_roomsScrollView == null || roomRowTemplate == null) return;

            _roomsScrollView.Clear();
            if (rooms == null || rooms.Count == 0) return;

            foreach (var room in rooms)
            {
                var roomRow = roomRowTemplate.CloneTree();
                var displayName = room.DisplayName.Value;
                var modeName = room.Mode;
                var mapName = room.Map;
                var isOpen = (bool)room.IsOpen;

                roomRow.tooltip = isOpen ? "Waiting for players" : "Match is in progress";

                var playIndicator = roomRow.Q<VisualElement>(UI_Room_Row_Template.play_indicator);
                playIndicator?.EnableInClassList("green", isOpen);

                var roomNameLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_name);
                if (roomNameLabel != null) roomNameLabel.text = displayName;

                var roomModeLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_mode);
                if (roomModeLabel != null) roomModeLabel.text = modeName;

                var roomMapLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_map);
                if (roomMapLabel != null) roomMapLabel.text = mapName;

                var playerCountLabel = roomRow.Q<Label>(UI_Room_Row_Template.player_count);
                if (playerCountLabel != null) playerCountLabel.text = $"{room.PlayerCount}/{room.MaxPlayers}";

                var enterBtn = roomRow.Q<Button>(UI_Room_Row_Template.enter_button);
                if (enterBtn != null)
                {
                    enterBtn.SetEnabled(isOpen && !room.IsFull);
                    enterBtn.clicked += () =>
                    {
                        enterBtn.SetEnabled(false);
                        OnEnterRoomClicked?.Invoke(room, displayName, modeName, mapName);
                    };
                }

                _roomsScrollView.Add(roomRow);
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
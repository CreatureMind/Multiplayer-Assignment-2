using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    private const string DisplayName = "DisplayName";
    private const string ModeName = "ModeName";
    private const string MapName = "MapName";

    [Header("Menus")]
    // commented out for assignment 3
    //[SerializeField] private VisualTreeAsset lobbiesListView;
    [SerializeField] private VisualTreeAsset roomsListView;
    [SerializeField] private VisualTreeAsset roomView;
    
    [Header("Templates")]
    // commented out for assignment 3
    //[SerializeField] private VisualTreeAsset sessionRowTemplate;
    [SerializeField] private VisualTreeAsset roomRowTemplate;
    [SerializeField] private VisualTreeAsset playerRowTemplate;
    
    [Header("Additive UIs")]
    [SerializeField] private UIDocument roomCreationViewPrefab;
    [SerializeField] private UIDocument loadingScreenViewPrefab;
    [SerializeField] private ChatUIController chatViewPrefab;
    
    [Header("UI Elements")]
    [SerializeField] private SessionsListDataSO sessionsListData;
    
    private UIDocument _uiDocument;
    private VisualElement _root;
    private VisualElement _roomsScrollView;
    private VisualElement _playerListScrollView;
    private ChatUIController _chatViewInstance;

    private bool _canSpin = true;

    private string _currentLobbyId;

    // Caching for client-side filtering
    private SessionDataRefreshedEvent? _cachedSessionData;
    private DropdownField _roomsDropdown;
    private DropdownField _modesDropdown;
    private DropdownField _mapsDropdown;
    
    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        // Name Entry
        EventBus.Subscribe<PlayerNameConfirmedEvent>(OnPlayerNameConfirmed);
        
        // Sessions List
        EventBus.Subscribe<JoinedLobbyEvent>(ShowRoomsListView);
        EventBus.Subscribe<SessionDataRefreshedEvent>(UpdateRoomsList);
        
        // Rooms List
        EventBus.Subscribe<RoomCreatedEvent>(ShowRoomView);
        
        // Loading Screen
        EventBus.Subscribe<ShowLoadingScreenEvent>(ShowLoadingScreen);
        EventBus.Subscribe<HideLoadingScreenEvent>(HideLoadingScreen);
        
        // Room view
        EventBus.Subscribe<PlayerListChangedEvent>(UpdatePlayerList);
        EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        
        //Ready manager
        EventBus.Subscribe<MatchStartedEvent>(StartMatch);
        
        //Chat
        EventBus.Subscribe<OnChatRelaySpawnedEvent>(CreateNewChat);
    }

    private void OnDisable()
    {
        // Name Entry
        EventBus.Unsubscribe<PlayerNameConfirmedEvent>(OnPlayerNameConfirmed);
        
        // Sessions List
        EventBus.Unsubscribe<JoinedLobbyEvent>(ShowRoomsListView);
        EventBus.Unsubscribe<SessionDataRefreshedEvent>(UpdateRoomsList);
        
        // Rooms List
        EventBus.Unsubscribe<RoomCreatedEvent>(ShowRoomView);
        
        // Loading Screen
        EventBus.Unsubscribe<ShowLoadingScreenEvent>(ShowLoadingScreen);
        EventBus.Unsubscribe<HideLoadingScreenEvent>(HideLoadingScreen);
        
        // Room view
        EventBus.Unsubscribe<PlayerListChangedEvent>(UpdatePlayerList);
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        
        //Ready manager
        EventBus.Unsubscribe<MatchStartedEvent>(StartMatch);
        
        //Chat
        EventBus.Unsubscribe<OnChatRelaySpawnedEvent>(CreateNewChat);
    }
    
    private void Start()
    {
        _uiDocument.visualTreeAsset = null;
    }
    
    private void OnPlayerNameConfirmed(PlayerNameConfirmedEvent e)
    {
        // commented out for assignment 3
        //ShowSessionsListView();
        EnterGlobalLobby();
    }

    private void StartMatch(MatchStartedEvent e)
    {
        EventBus.Raise(new ShowLoadingScreenEvent());
    }

    // commented out for assignment 3
    /*private void ShowSessionsListView()
    {
        _uiDocument.visualTreeAsset = lobbiesListView;
        _root = _uiDocument.rootVisualElement;

        var scrollView = _root.Q<ScrollView>(UI_Lobbies_List_View.sessions_scroll_view);                                       //sessions-scroll-view
        if (scrollView == null)
        {
            Debug.LogError("Could not find ScrollView named 'session-scroll-view' in sessionsListView.");
            return;
        }

        UpdateLobbyList(scrollView);
    }*/
    
    // commented out for assignment 3
    /*private void UpdateLobbyList(ScrollView scrollView)
    {
        scrollView.Clear();
        
        if (!sessionsListData)
        {
            Debug.Log("Sessions list data empty");
            return;
        }
        
        var availableSessions = sessionsListData.sessionsList;

        foreach (var session in availableSessions)
        {
            var sessionRow = sessionRowTemplate.CloneTree();
            
            var sessionNameLabel = sessionRow.Q<Label>(UI_Session_Row_Template.lobby_name);                                       //lobby-name
            if (sessionNameLabel != null)
            {
                sessionNameLabel.text = session.sessionName;
            }

            var enterBtn = sessionRow.Q<Button>(UI_Session_Row_Template.enter_button);                                            //enter-button
            if (enterBtn != null)
            {
                enterBtn.clicked += () =>
                {
                    if (!NetworkManager.Instance) return;
                    
                    enterBtn.SetEnabled(false);
                    _ = NetworkManager.Instance.ConnectToCustomLobby(session.sessionName);
                    _currentLobbyId = session.sessionName;
                };
            }
            
            scrollView.Add(sessionRow);
        }
    }*/

    private void EnterGlobalLobby()
    {
        var session = sessionsListData.sessionsList[0];
        
        if (!NetworkManager.Instance) return;
        
        _ = NetworkManager.Instance.ConnectToCustomLobby(session.sessionName);
        _currentLobbyId = session.sessionName;
    }

    private void ShowRoomsListView(JoinedLobbyEvent e)
    {
        // Sync from the persistent manager (set even when returning from a match).
        if (NetworkManager.Instance && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentLobbyId))
            _currentLobbyId = NetworkManager.Instance.CurrentLobbyId;

        _uiDocument.visualTreeAsset = roomsListView;
        _root = _uiDocument.rootVisualElement;
        
        var headerLabel = _root.Q<Label>(UI_Rooms_List_View_v3.header);                                                          //header
        headerLabel.text = _currentLobbyId + " / Rooms";

        // Assignment 3
        _roomsDropdown = _root.Q<DropdownField>(UI_Rooms_List_View_v3.rooms_dropdown);
        _modesDropdown = _root.Q<DropdownField>(UI_Rooms_List_View_v3.modes_dropdown);
        _mapsDropdown = _root.Q<DropdownField>(UI_Rooms_List_View_v3.maps_dropdown);
        
        _roomsDropdown?.RegisterValueChangedCallback(evt => ApplyRoomFilters());

        _modesDropdown?.RegisterValueChangedCallback(evt => ApplyRoomFilters());

        _mapsDropdown?.RegisterValueChangedCallback(evt => ApplyRoomFilters());

        SetRoomsListButtons(_root);
        
        _roomsScrollView = _root.Q<ScrollView>(UI_Rooms_List_View_v3.rooms_scroll_view);                                        //rooms-scroll-view
        if (_roomsScrollView == null)
            Debug.LogError("Could not find ScrollView named 'rooms-scroll-view' in roomsListView.");
    }

    private void SetRoomsListButtons(VisualElement root)
    {
        // commented out for assignment 3
        /*var leaveBtn = root.Q<Button>(UI_Rooms_List_View.leave_button);                                                      //leave-button
        if (leaveBtn != null)
        {
            leaveBtn.clicked+= ShowSessionsListView;
        }*/
        
        var createRoomBtn = root.Q<Button>(UI_Rooms_List_View.create_button);                                                //create-button
        if (createRoomBtn != null)
        {
            createRoomBtn.clicked += ShowRoomCreationView;
        }

        var refreshBtn = root.Q<Button>(UI_Rooms_List_View.refresh_button);                                                  //refresh-button
        if (refreshBtn != null)
        {
            refreshBtn.clicked += () =>
            {
                if (!NetworkManager.Instance) return;
                    
                refreshBtn.SetEnabled(false);
                _ = NetworkManager.Instance.ConnectToCustomLobby(_currentLobbyId);
            };
        }
    }
    
    private void UpdateRoomsList(SessionDataRefreshedEvent e)
    {
        // Cache the session data for client-side filtering
        _cachedSessionData = e;

        // Apply filters and render
        ApplyRoomFilters();
    }

    private void RenderRoomsList(List<SessionInfo> rooms, int totalPlayers)
    {
        _roomsScrollView.Clear();

        UpdatePlayerCountInLobby(totalPlayers);

        if (rooms.Count == 0) return;

        foreach (var room in rooms)
        {
            var roomRow = roomRowTemplate.CloneTree();
            
            room.Properties.TryGetValue(DisplayName, out var displayName);
            room.Properties.TryGetValue(ModeName, out var modeName);
            room.Properties.TryGetValue(MapName, out var mapName);
            
            roomRow.tooltip = room.IsOpen ? "Waiting for players" : "Match is in progress";
            
            var playIndicator = roomRow.Q<VisualElement>(UI_Room_Row_Template.play_indicator);
            playIndicator?.EnableInClassList("green", room.IsOpen);

            var roomNameLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_name);                                              //room-name
            if (roomNameLabel != null)
            {
                roomNameLabel.text = displayName;
            }
            
            var roomModeLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_mode);
            if (roomModeLabel != null)
            {
                roomModeLabel.text = modeName;
            }
            
            var roomMapLabel = roomRow.Q<Label>(UI_Room_Row_Template.room_map);
            if (roomMapLabel != null)
            {
                roomMapLabel.text = mapName;
            }

            var enterBtn = roomRow.Q<Button>(UI_Room_Row_Template.enter_button);                                               //enter-button
            if (enterBtn != null)
            {
                var isFull = room.PlayerCount >= room.MaxPlayers;
                enterBtn.SetEnabled(!isFull);
                enterBtn.SetEnabled(room.IsOpen);
                
                enterBtn.clicked += () =>
                {
                    if (!NetworkManager.Instance) return;
                    
                    enterBtn.SetEnabled(false);
                    _ = NetworkManager.Instance.JoinRoom(room.Name);
                    ShowRoomView(displayName, modeName, mapName);
                };
            }
            
            var playerCountLabel = roomRow.Q<Label>(UI_Room_Row_Template.player_count);                                         //player-count
            if (playerCountLabel != null) playerCountLabel.text = $"{room.PlayerCount}/{room.MaxPlayers}";
            
            _roomsScrollView.Add(roomRow);
        }
    }
    
    private void UpdatePlayerCountInLobby(int totalPlayers)
    {
        var playerCountLabel = _root.Q<Label>(UI_Rooms_List_View.online_label);                                              //online-label
        if (playerCountLabel != null) playerCountLabel.text = $"Online Players: {totalPlayers}";
    }

    // Assignment 3
    private void ApplyRoomFilters()
    {
        if (!_cachedSessionData.HasValue) return;

        var allRooms = _cachedSessionData.Value.Sessions;
        var filteredRooms = new List<SessionInfo>();

        // Get current dropdown values
        var selectedRoomFilter = _roomsDropdown?.value ?? "All";
        var selectedModeFilter = _modesDropdown?.value ?? "All";
        var selectedMapFilter = _mapsDropdown?.value ?? "All";

        foreach (var room in allRooms)
        {
            // Get room properties
            room.Properties.TryGetValue(ModeName, out var modeName);
            room.Properties.TryGetValue(MapName, out var mapName);

            // Apply filters
            var matchesRoomFilter = selectedRoomFilter == "All" ||
                                    (selectedRoomFilter == "Open" && room.IsOpen) ||
                                    (selectedRoomFilter == "Closed" && !room.IsOpen);

            var matchesModeFilter = selectedModeFilter == "All" || modeName == selectedModeFilter;
            var matchesMapFilter = selectedMapFilter == "All" || mapName == selectedMapFilter;

            if (matchesRoomFilter && matchesModeFilter && matchesMapFilter)
            {
                filteredRooms.Add(room);
            }
        }

        // Update the UI with filtered rooms
        RenderRoomsList(filteredRooms, _cachedSessionData.Value.TotalPlayers);
    }

    private void ShowRoomCreationView()
    {
        roomCreationViewPrefab.gameObject.SetActive(true);
        var root = roomCreationViewPrefab.rootVisualElement;

        var roomNameField = root.Q<TextField>(UI_Room_Creation_View.room_name);                                                 //room-name
        var maxPlayersField = root.Q<SliderInt>(UI_Room_Creation_View.max_players);                                             //max-players
        //Assignment 3
        var modesDropdown = root.Q<DropdownField>(UI_Room_Creation_View.modes_dropdown);
        var mapsDropdown = root.Q<DropdownField>(UI_Room_Creation_View.maps_dropdown);
        var publicToggle = root.Q<Toggle>(UI_Room_Creation_View.public_toggle);
        
        var createBtn = root.Q<Button>(UI_Room_Creation_View.create_button);                                                    //create-button
        if (createBtn != null)
        {
            createBtn.clicked += () =>
            {
                if (!NetworkManager.Instance) return;
                
                if (string.IsNullOrEmpty(roomNameField.value)) return;

                createBtn.SetEnabled(false);
                var roomName = roomNameField.value;
                var maxPlayers = maxPlayersField.value;
                //Assignment 3
                var chosenMode = modesDropdown.value;
                var chosenMap = mapsDropdown.value;
                var isPublic = publicToggle.value;
                Debug.Log("Creating room with name: " + roomName + " and max players: " + maxPlayers + " and mode: " + chosenMode + " and map: " + chosenMap + " and isPublic: " + isPublic);
                
                _ = NetworkManager.Instance.CreateRoomInCurrentLobby(roomName, maxPlayers, _currentLobbyId, chosenMode, chosenMap, isPublic);

                roomCreationViewPrefab.gameObject.SetActive(false);
            };
        }
        
        var backBtn = root.Q<Button>(UI_Room_Creation_View.back_button);                                                        //back-button
        if (backBtn != null)
        {
            backBtn.clicked += () =>
            {
                roomCreationViewPrefab.gameObject.SetActive(false);
            };
        }
    }
    
    private void ShowRoomView(RoomCreatedEvent e)
    {
        SetRoom(e.RoomName, e.ModeName, e.MapName);
    }    
    
    private void ShowRoomView(string roomName, string modeName, string mapName)
    {
        SetRoom(roomName, modeName, mapName);
    }

    private void SetRoom(string roomName, string modeName, string mapName)
    {
        _uiDocument.visualTreeAsset = roomView;
        _root = _uiDocument.rootVisualElement;
        
        var headerLabel = _root.Q<Label>(UI_Room_View.header);                                                        //header
        if (headerLabel != null)
        {
            headerLabel.text = roomName + " / " + modeName + " / " + mapName;
        }
        
        _playerListScrollView = _root.Q<ScrollView>(UI_Room_View.players_scroll_view);                                        //rooms-scroll-view
        if (_playerListScrollView == null)
            Debug.LogError("Could not find ScrollView named 'players-scroll-view' in roomsListView.");
        
        var leaveBtn = _root.Q<Button>(UI_Room_View.leave_button);                                                     //leave-button
        if (leaveBtn != null)
        {
            leaveBtn.clicked += async () =>
            {
                if (!NetworkManager.Instance) return;

                leaveBtn.SetEnabled(false);
                await NetworkManager.Instance.LeaveRoom(_currentLobbyId);
            };
        }
        
        var readyBtn = _root.Q<Button>(UI_Room_View.ready_button);                                                     //ready-button
        if (readyBtn != null)
        {
            readyBtn.clicked += () =>
            {
                if (!NetworkManager.Instance) return;
                
                var data = NetworkManager.Instance.GetLocalPlayerData();
                if (!data) return;
                NetworkManager.Instance.SetLocalPlayerReady(!data.IsReady);
            };
        }
        
        var startBtn = _root.Q<Button>(UI_Room_View.start_button);
        if (startBtn != null)
        {
            startBtn.clicked += () =>
            {
                if (NetworkManager.Instance?.ReadyManagerInstance is { } rm)
                    rm.StartMatch(modeName, mapName);
            };
        }
        RefreshStartButton();
    }
    
    private void UpdatePlayerList(PlayerListChangedEvent e)
    {
        if (_playerListScrollView == null) return;
        _playerListScrollView.Clear();
        
        var playerNameList = new List<string>();

        foreach (var playerData in NetworkManager.Instance.GetAllPlayers())
        {
            if (playerData == null || !playerData.Object || !playerData.Object.IsValid) continue;
            
            var row = playerRowTemplate.CloneTree();

            var nameLabel = row.Q<Label>(UI_Player_Row_Template.player_name);                                                    //player-name
            if (nameLabel != null)
                nameLabel.text = playerData.DisplayName.Value;

            var readyLabel = row.Q<Label>(UI_Player_Row_Template.ready_status);                                                  //ready-status
            if (readyLabel != null)
                readyLabel.text = playerData.IsReady ? "is Ready!" : "is Not Ready.";
            
            var kickBtn = row.Q<Button>(UI_Player_Row_Template.kick_button);                                                     //kick-button
            if (kickBtn != null)
            {
                if (!NetworkManager.Instance) return;
                
                var canKick = NetworkManager.Instance.CanKick();
                var isSelf = NetworkManager.Instance.IsLocalPlayer(playerData.Object.InputAuthority);
            
                kickBtn.style.display = (canKick && !isSelf) 
                    ? DisplayStyle.Flex 
                    : DisplayStyle.None;

                kickBtn.clicked += () =>
                    NetworkManager.Instance.KickPlayer(playerData.Object.InputAuthority);
            }

            _playerListScrollView.Add(row);
            playerNameList.Add(playerData.DisplayName.Value);
            
            RefreshReadyButton();
            RefreshStartButton();
        }
        
        EventBus.Raise(new OnPlayerListChangedEvent
        {
            PlayerNames = playerNameList
        });
    }

    private void CreateNewChat(OnChatRelaySpawnedEvent e)
    {
        if (_chatViewInstance) return;
        
        _chatViewInstance = Instantiate(chatViewPrefab);
    }
    
    private void OnPlayerDataChanged(PlayerDataChangedEvent e)
    {
        UpdatePlayerList(new PlayerListChangedEvent());
    }
    
    private void RefreshReadyButton()
    {
        var readyBtn = _root?.Q<Button>(UI_Room_View.ready_button);
        if (readyBtn == null) return;
        
        var data = NetworkManager.Instance?.GetLocalPlayerData();
        if (!data) return;
        
        readyBtn.text = data.IsReady ? "Not Ready" : "Ready";
    }
    
    private void RefreshStartButton()
    {
        if (_root == null || !NetworkManager.Instance) return;

        var startBtn = _root.Q<Button>(UI_Room_View.start_button);
        if (startBtn == null) return;

        var isMaster = NetworkManager.Instance.CanStartGame();
        var allReady = NetworkManager.Instance.AreAllPlayersReady();

        startBtn.style.display = isMaster ? DisplayStyle.Flex : DisplayStyle.None;
        startBtn.SetEnabled(allReady);
    }

    private void ShowLoadingScreen(ShowLoadingScreenEvent e)
    {
        loadingScreenViewPrefab.gameObject.SetActive(true);
        
        var root = loadingScreenViewPrefab.rootVisualElement;
        
        var loadingSpinner = root.Q<VisualElement>(UI_Loading_View.loading_spinner);                                     //loading-spinner
        if (loadingSpinner != null)
        {
            SpinLoading(loadingSpinner);
        }
    }
    
    private void SpinLoading(VisualElement loadingSpinner)
    {
        _canSpin = true;
        
        loadingSpinner.schedule.Execute(() => {
            var currentAngle = loadingSpinner.style.rotate.value.angle.value;
            loadingSpinner.style.rotate = new Rotate(currentAngle + 360f);
        }).Every(16).Until((() => !_canSpin));
    }
    
    private void HideLoadingScreen(HideLoadingScreenEvent e)
    {
        loadingScreenViewPrefab.gameObject.SetActive(false);
        _canSpin = false;
    }
}

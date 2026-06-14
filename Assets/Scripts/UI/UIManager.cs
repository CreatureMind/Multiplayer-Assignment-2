using Events;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    [Header("Menus")]
    [SerializeField] private VisualTreeAsset lobbiesListView;
    [SerializeField] private VisualTreeAsset roomsListView;
    [SerializeField] private VisualTreeAsset roomView;
    
    [Header("Templates")]
    [SerializeField] private VisualTreeAsset sessionRowTemplate;
    [SerializeField] private VisualTreeAsset roomRowTemplate;
    [SerializeField] private VisualTreeAsset playerRowTemplate;
    
    [Header("Additive UIs")]
    [SerializeField] private GameObject roomCreationViewPrefab;
    [SerializeField] private GameObject loadingScreenViewPrefab;
    
    [Header("UI Elements")]
    [SerializeField] private SessionsListDataSO sessionsListData;
    
    private UIDocument _uiDocument;
    private UIDocument _roomCreationView;
    private UIDocument _loadingScreenView;
    private VisualElement _root;
    private VisualElement _roomsScrollView;
    private VisualElement _playerListScrollView;
    
    private bool _canSpin = true;
    
    private string _currentLobbyId;
    
    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _roomCreationView = roomCreationViewPrefab.GetComponent<UIDocument>();
        _loadingScreenView = loadingScreenViewPrefab.GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
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
    }

    private void OnDisable()
    {
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
    }
    
    private void Start()
    {
        if (lobbiesListView)
        {
            ShowSessionsListView();
        }
    }

    private void StartMatch(MatchStartedEvent e)
    {
        SceneManager.LoadScene("Game_Scene");
    }

    private void ShowSessionsListView()
    {
        _uiDocument.visualTreeAsset = lobbiesListView;
        _root = _uiDocument.rootVisualElement;

        var scrollView = _root.Q<ScrollView>("sessions-scroll-view");                                       //sessions-scroll-view
        if (scrollView == null)
        {
            Debug.LogError("Could not find ScrollView named 'session-scroll-view' in sessionsListView.");
            return;
        }

        UpdateLobbyList(scrollView);
    }
    
    private void UpdateLobbyList(ScrollView scrollView)
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
            
            var sessionNameLabel = sessionRow.Q<Label>("lobby-name");                                       //lobby-name
            if (sessionNameLabel != null)
            {
                sessionNameLabel.text = session.sessionName;
            }

            var enterBtn = sessionRow.Q<Button>("enter-button");                                            //enter-button
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
    }

    private void ShowRoomsListView(JoinedLobbyEvent e)
    {
        _uiDocument.visualTreeAsset = roomsListView;
        _root = _uiDocument.rootVisualElement;
        
        var headerLabel = _root.Q<Label>("header");                                                          //header
        headerLabel.text = _currentLobbyId + " / Rooms";
        
        SetRoomsListButtons(_root);
        
        _roomsScrollView = _root.Q<ScrollView>("rooms-scroll-view");                                        //rooms-scroll-view
        if (_roomsScrollView == null)
            Debug.LogError("Could not find ScrollView named 'rooms-scroll-view' in roomsListView.");
    }

    private void SetRoomsListButtons(VisualElement root)
    {
        var leaveBtn = root.Q<Button>("leave-button");                                                      //leave-button
        if (leaveBtn != null)
        {
            leaveBtn.clicked += ShowSessionsListView;
        }
        
        var createRoomBtn = root.Q<Button>("create-button");                                                //create-button
        if (createRoomBtn != null)
        {
            createRoomBtn.clicked += ShowRoomCreationView;
        }

        var refreshBtn = root.Q<Button>("refresh-button");                                                  //refresh-button
        if (refreshBtn != null)
        {
            refreshBtn.clicked += () =>
            {
                refreshBtn.SetEnabled(false);
            };
        }
    }
    
    private void UpdateRoomsList(SessionDataRefreshedEvent e)
    {
        _roomsScrollView.Clear();
        
        UpdatePlayerCountInLobby(e.TotalPlayers);
        
        if (e.Sessions.Count == 0) return;
        
        foreach (var room in e.Sessions)
        {
            var roomRow = roomRowTemplate.CloneTree();
            
            var displayName= room.Properties.TryGetValue("DisplayName", out var dn);
            
            var roomNameLabel = roomRow.Q<Label>("room-name");                                              //room-name
            if (roomNameLabel != null)
            {
                roomNameLabel.text = dn;
            }

            var enterBtn = roomRow.Q<Button>("enter-button");                                               //enter-button
            if (enterBtn != null)
            {
                var isFull = room.PlayerCount >= room.MaxPlayers;
                enterBtn.SetEnabled(!isFull);
                
                enterBtn.clicked += () =>
                {
                    if (!NetworkManager.Instance) return;
                    
                    enterBtn.SetEnabled(false);
                    _ = NetworkManager.Instance.JoinRoom(room.Name);
                    ShowRoomView(dn);
                };
            }
            
            var playerCountLabel = roomRow.Q<Label>("player-count");                                         //player-count
            if (playerCountLabel != null) playerCountLabel.text = $"{room.PlayerCount}/{room.MaxPlayers}";
            
            _roomsScrollView.Add(roomRow);
        }
    }
    
    private void UpdatePlayerCountInLobby(int totalPlayers)
    {
        var playerCountLabel = _root.Q<Label>("online-label");                                              //online-label
        if (playerCountLabel != null) playerCountLabel.text = $"Online Players: {totalPlayers}";
    }

    private void ShowRoomCreationView()
    {
        roomCreationViewPrefab.SetActive(true);
        var root = _roomCreationView.rootVisualElement;

        var roomNameField = root.Q<TextField>("room-name");                                                 //room-name
        var maxPlayersField = root.Q<SliderInt>("max-players");                                             //max-players

        var createBtn = root.Q<Button>("create-button");                                                    //create-button
        if (createBtn != null)
        {
            createBtn.clicked += () =>
            {
                if (!NetworkManager.Instance) return;

                createBtn.SetEnabled(false);
                var roomName = roomNameField.value;
                var maxPlayers = maxPlayersField.value;
                _ = NetworkManager.Instance.CreateRoomInCurrentLobby(roomName, maxPlayers, _currentLobbyId);

                roomCreationViewPrefab.SetActive(false);
            };
        }
        
        var backBtn = root.Q<Button>("back-button");                                                        //back-button
        if (backBtn != null)
        {
            backBtn.clicked += () =>
            {
                roomCreationViewPrefab.SetActive(false);
            };
        }
    }
    
    private void ShowRoomView(RoomCreatedEvent e)
    {
        SetRoom(e.RoomName);
    }    
    
    private void ShowRoomView(string roomName)
    {
        SetRoom(roomName);
    }

    private void SetRoom(string roomName)
    {
        _uiDocument.visualTreeAsset = roomView;
        _root = _uiDocument.rootVisualElement;
        
        var headerLabel = _root.Q<Label>("header");                                                        //header
        if (headerLabel != null)
        {
            headerLabel.text = roomName;
        }
        
        _playerListScrollView = _root.Q<ScrollView>("players-scroll-view");                                        //rooms-scroll-view
        if (_playerListScrollView == null)
            Debug.LogError("Could not find ScrollView named 'players-scroll-view' in roomsListView.");
        
        var leaveBtn = _root.Q<Button>("leave-button");                                                     //leave-button
        if (leaveBtn != null)
        {
            leaveBtn.clicked += async () =>
            {
                if (!NetworkManager.Instance) return;

                leaveBtn.SetEnabled(false);
                await NetworkManager.Instance.LeaveRoom(_currentLobbyId);
            };
        }
        
        var readyBtn = _root.Q<Button>("ready-button");                                                     //ready-button
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
        
        var startBtn = _root.Q<Button>("start-button");
        if (startBtn != null)
        {
            startBtn.clicked += () =>
            {
                if (NetworkManager.Instance?.ReadyManagerInstance is { } rm)
                    rm.StartMatch();
            };
        }
        RefreshStartButton();
    }
    
    private void UpdatePlayerList(PlayerListChangedEvent e)
    {
        if (_playerListScrollView == null) return;
        _playerListScrollView.Clear();

        foreach (var playerData in NetworkManager.Instance.GetAllPlayers())
        {
            var row = playerRowTemplate.CloneTree();

            var nameLabel = row.Q<Label>("player-name");                                                    //player-name
            if (nameLabel != null)
                nameLabel.text = playerData.DisplayName.Value;

            var readyLabel = row.Q<Label>("ready-status");                                                  //ready-status
            if (readyLabel != null)
                readyLabel.text = playerData.IsReady ? "is Ready!" : "is Not Ready.";
            
            var kickBtn = row.Q<Button>("kick-button");                                                     //kick-button
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
            
            RefreshReadyButton();
            RefreshStartButton();
        }
    }
    
    private void OnPlayerDataChanged(PlayerDataChangedEvent e)
    {
        UpdatePlayerList(new PlayerListChangedEvent());
    }
    
    private void RefreshReadyButton()
    {
        var readyBtn = _root?.Q<Button>("ready-button");
        if (readyBtn == null) return;
        
        var data = NetworkManager.Instance?.GetLocalPlayerData();
        if (!data) return;
        
        readyBtn.text = data.IsReady ? "Not Ready" : "Ready";
    }
    
    private void RefreshStartButton()
    {
        if (_root == null || !NetworkManager.Instance) return;

        var startBtn = _root.Q<Button>("start-button");
        if (startBtn == null) return;

        var isMaster = NetworkManager.Instance.CanStartGame();
        var allReady = NetworkManager.Instance.AreAllPlayersReady();

        startBtn.style.display = isMaster ? DisplayStyle.Flex : DisplayStyle.None;
        startBtn.SetEnabled(allReady);
    }

    private void ShowLoadingScreen(ShowLoadingScreenEvent e)
    {
        loadingScreenViewPrefab.SetActive(true);
        
        var root = _loadingScreenView.rootVisualElement;
        
        var loadingSpinner = root.Q<VisualElement>("loading-spinner");                                     //loading-spinner
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
        loadingScreenViewPrefab.SetActive(false);
        _canSpin = false;
    }
}

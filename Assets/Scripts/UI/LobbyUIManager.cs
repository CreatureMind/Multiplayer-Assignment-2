using System.Collections.Generic;
using Events;
using UI.Dialog;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;
using UI.MainMenu;
using UI.Options;
using UI.RoomsList;
using UI.RoomLobby;
using UI.RoomCreation;
using UI.Loading;
using UI.NameEntry;

namespace UI
{
    public enum LobbyScreen
    {
        None,
        MainMenu,
        RoomsList,
        RoomLobby
    }
    
    public class LobbyUIManager : MonoBehaviour
    {
        [Header("Full Screen Views")]
        [SerializeField] private MainMenuUIView     mainMenuView;
        [SerializeField] private NameEntryUIView    nameEntryView;
        [SerializeField] private RoomsListUIView    roomsListView;
        [SerializeField] private RoomLobbyUIView    roomLobbyView;
        
        [Header("Overlays")]
        [SerializeField] private OptionsUIView      optionsView;
        [SerializeField] private RoomCreationUIView roomCreationView;
        [SerializeField] private LoadingUIView      loadingViewPrefab;
        [SerializeField] private ChatUIController   chatViewPrefab;
        [SerializeField] private DialogUIView       dialogView;

        [Header("Audio Settings")]
        [SerializeField] private AudioMixer audioMixer;

        // Presenters
        private MainMenuUIPresenter     _mainMenuPresenter;
        private NameEntryUIPresenter    _nameEntryPresenter;
        private OptionsUIPresenter      _optionsPresenter;
        private RoomsListUIPresenter    _roomsListPresenter;
        private RoomLobbyUIPresenter    _roomLobbyPresenter;
        private RoomCreationUIPresenter _roomCreationPresenter;
        private LoadingUIPresenter      _loadingPresenter;
        private DialogUIPresenter       _dialogPresenter;
        
        // Chat
        private ChatUIController _chatViewInstance;
        
        //Loading
        private LoadingUIView _loadingViewInstance;

        private void Start()
        {
            InitializePresenters();
            DetermineInitialFlow();
        }

        private void InitializePresenters()
        {
            var lobbyId = NetworkManager.Instance ? NetworkManager.Instance.CurrentLobbyId : string.Empty;

            //Main Menu
            var mainMenuModel = new MainMenuUIModel();
            _mainMenuPresenter = new MainMenuUIPresenter(
                mainMenuModel, 
                mainMenuView,
                onPlayRequested: () => ShowScreen(LobbyScreen.None),
                onOptionsRequested: () => optionsView.Show(),
                onCreditsRequested: () => { }
            );
            
            //Name Entry
            var nameEntryModel = new NameEntryUIModel();
            _nameEntryPresenter = new NameEntryUIPresenter(
                nameEntryModel,
                nameEntryView,
                StartCoroutine
            );

            //Options
            var optionsModel = new OptionsUIModel(audioMixer);
            _optionsPresenter = new OptionsUIPresenter(
                optionsModel, 
                optionsView
            );

            //Rooms List
            var roomsListModel = new RoomsListUIModel();
            _roomsListPresenter = new RoomsListUIPresenter(
                roomsListModel,
                roomsListView,
                onLeaveRequested: () =>
                {
                    EventBus.Raise(new HideChatEvent());
                    ShowScreen(LobbyScreen.MainMenu);
                },
                onCreateRoomRequested: () => roomCreationView.Show(),
                onJoinedRoomViewRequested: (roomName, mode, map) => 
                {
                    _roomLobbyPresenter.SetupRoomDetails(roomName, mode, map);
                    ShowScreen(LobbyScreen.RoomLobby);
                }
            );

            //Room Creation Overlay
            var roomCreationModel = new RoomCreationUIModel();
            _roomCreationPresenter = new RoomCreationUIPresenter(
                roomCreationModel,
                roomCreationView,
                lobbyId,
                onRoomCreatedRequested: () =>
                {
                    ShowScreen(LobbyScreen.None);
                }
            );

            //Room Lobby
            var roomLobbyModel = new RoomLobbyUIModel();
            _roomLobbyPresenter = new RoomLobbyUIPresenter(
                roomLobbyModel,
                roomLobbyView,
                lobbyId
            );

            //Loading Overlay
            SpawnLoadingOverlay();
            
            //Dialog Overlay
            var dialogModel = new DialogUIModel();
            _dialogPresenter = new DialogUIPresenter(dialogModel, dialogView);
            
            SubscribeToGlobalEvents();
        }
        
        private void SubscribeToGlobalEvents()
        {
            EventBus.Subscribe<PlayerNameConfirmedEvent>(OnPlayerNameConfirmed);
            EventBus.Subscribe<JoinedLobbyEvent>        (OnJoinedLobbyHub);
            EventBus.Subscribe<JoinedRoomEvent>         (OnJoinedRoom);
            EventBus.Subscribe<OnChatRelaySpawnedEvent> (CreateNewChat);
            EventBus.Subscribe<MatchStartedEvent>       (StartMatch);
            EventBus.Subscribe<ReturnToMainMenuEvent>   (ShowMainMenu);
        }

        private void UnsubscribeGlobalEvents()
        {
            EventBus.Unsubscribe<PlayerNameConfirmedEvent>(OnPlayerNameConfirmed);
            EventBus.Unsubscribe<JoinedLobbyEvent>        (OnJoinedLobbyHub);
            EventBus.Unsubscribe<JoinedRoomEvent>         (OnJoinedRoom);
            EventBus.Unsubscribe<OnChatRelaySpawnedEvent> (CreateNewChat);
            EventBus.Unsubscribe<MatchStartedEvent>       (StartMatch);
            EventBus.Unsubscribe<ReturnToMainMenuEvent>   (ShowMainMenu);
        }
        
        private void StartMatch(MatchStartedEvent e)
        {
            EventBus.Raise(new ShowLoadingScreenEvent());
            HideAllScreens();
        }

        private void ShowMainMenu(ReturnToMainMenuEvent e)
        {
            HideAllScreens();
            ShowScreen(LobbyScreen.MainMenu);
        }

        private void DetermineInitialFlow()
        {
            // If name is already confirmed (or returning from match), go to Main Menu directly
            if (NetworkManager.Instance && NetworkManager.Instance.IsReturningFromMatch)
            {
                ShowScreen(LobbyScreen.RoomsList);
            }
            else
            {
                ShowScreen(LobbyScreen.MainMenu);
            }
        }
        
        private void OnJoinedLobbyHub(JoinedLobbyEvent e) => _nameEntryPresenter.Initialize();

        private void OnPlayerNameConfirmed(PlayerNameConfirmedEvent e) => ShowScreen(LobbyScreen.RoomsList);

        private void OnJoinedRoom(JoinedRoomEvent e) => ShowScreen(LobbyScreen.RoomLobby);

        private void ShowScreen(LobbyScreen screen)
        {
            UIOverlaySorter.Reset();

            // Full-Screen visibility standard using Show() / Hide()
            if (screen == LobbyScreen.MainMenu)  mainMenuView.Show();  else mainMenuView.Hide();
            if (screen == LobbyScreen.RoomsList) roomsListView.Show(); else roomsListView.Hide();
            if (screen == LobbyScreen.RoomLobby) roomLobbyView.Show(); else roomLobbyView.Hide();
        }
        
        private void HideAllScreens()
        {
            mainMenuView    .Hide();
            roomsListView   .Hide();
            roomLobbyView   .Hide();
            nameEntryView   .Hide();
            optionsView     .Hide();
            roomCreationView.Hide();
        }
        
        private void CreateNewChat(OnChatRelaySpawnedEvent e)
        {
            if (_chatViewInstance) return;
        
            _chatViewInstance = Instantiate(chatViewPrefab);
        }

        private void SpawnLoadingOverlay()
        {
            if (_loadingViewInstance) return;
            _loadingViewInstance = Instantiate(loadingViewPrefab);
            _loadingViewInstance.gameObject.SetActive(true);
            
            var loadingModel = new LoadingUIModel();
            _loadingPresenter = new LoadingUIPresenter(loadingModel, _loadingViewInstance);
            DontDestroyOnLoad(_loadingViewInstance);
        }

        private void OnDestroy()
        {
            UnsubscribeGlobalEvents();
            
            _mainMenuPresenter?.    UnsubscribeFromEvents();
            _nameEntryPresenter?.   UnsubscribeFromEvents();
            _optionsPresenter?.     UnsubscribeFromEvents();
            _roomsListPresenter?.   UnsubscribeFromEvents();
            _roomCreationPresenter?.UnsubscribeFromEvents();
            _roomLobbyPresenter?.   UnsubscribeFromEvents();
            _loadingPresenter?.     UnsubscribeFromEvents();
            _dialogPresenter?.      UnsubscribeFromEvents();
        }
    }
}
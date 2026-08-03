using System;
using Events;
using UnityEngine;

namespace UI.RoomsList
{
    public class RoomsListUIPresenter
    {
        private readonly RoomsListUIModel _model;
        private readonly RoomsListUIView  _view;
        
        private readonly Action                         _onLeaveRequested;
        private readonly Action                         _onCreateRoomRequested;
        private readonly Action<string, string, string> _onJoinedRoomViewRequested;

        private string _currentLobbyId;

        public RoomsListUIPresenter(
            RoomsListUIModel model,
            RoomsListUIView view,
            Action onLeaveRequested = null,
            Action onCreateRoomRequested = null,
            Action<string, string, string> onJoinedRoomViewRequested = null)
        {
            _model = model;
            _view = view;
            _onLeaveRequested = onLeaveRequested;
            _onCreateRoomRequested = onCreateRoomRequested;
            _onJoinedRoomViewRequested = onJoinedRoomViewRequested;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnFilterChanged     += ApplyFiltersAndRender;
            _view.OnLeaveClicked      += HandleLeaveClicked;
            _view.OnCreateRoomClicked += HandleCreateRoomClicked;
            _view.OnRefreshClicked    += HandleRefreshClicked;
            _view.OnJoinRoomClicked   += HandleJoinRoomClicked;
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<JoinedLobbyEvent>    (OnJoinedLobby);
            EventBus.Subscribe<RoomListChangedEvent>(OnRoomListChanged);
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnFilterChanged     -= ApplyFiltersAndRender;
            _view.OnLeaveClicked      -= HandleLeaveClicked;
            _view.OnCreateRoomClicked -= HandleCreateRoomClicked;
            _view.OnRefreshClicked    -= HandleRefreshClicked;
            _view.OnJoinRoomClicked   -= HandleJoinRoomClicked;

            EventBus.Unsubscribe<JoinedLobbyEvent>    (OnJoinedLobby);
            EventBus.Unsubscribe<RoomListChangedEvent>(OnRoomListChanged);
        }

        private void OnJoinedLobby(JoinedLobbyEvent e)
        {
            if (NetworkManager.Instance && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentLobbyId))
            {
                _currentLobbyId = NetworkManager.Instance.CurrentLobbyId;
            }

            _view.SetHeader($"Tiny Soldiers / {_currentLobbyId} / Rooms");
            _view.SetRefreshButtonEnabled(true);
        }

        private void OnRoomListChanged(RoomListChangedEvent e)
        {
            _model.UpdateCachedRooms(e);
            _view.SetRefreshButtonEnabled(true);
            
            var totalPlayers = _model.GetTotalActivePlayers(e.TotalPlayers);
            _view.SetOnlinePlayerCount(totalPlayers);

            ApplyFiltersAndRender();
        }

        private void ApplyFiltersAndRender()
        {
            var filtered = _model.GetFilteredRooms(
                _view.SelectedRoomFilter,
                _view.SelectedModeFilter,
                _view.SelectedMapFilter
            );

            _view.RenderRoomsList(filtered);
        }

        private void HandleRefreshClicked()
        {
            _view.SetRefreshButtonEnabled(false);
            _model.RefreshLobby(_currentLobbyId);
        }

        private void HandleJoinRoomClicked(RoomInfo room, string displayName, string modeName, string mapName)
        {
            _model.JoinRoom(room.SessionName);
            _onJoinedRoomViewRequested?.Invoke(displayName, modeName, mapName);
        }

        private void HandleLeaveClicked()
        {
            _onLeaveRequested?.Invoke();
        }

        private void HandleCreateRoomClicked()
        {
            _onCreateRoomRequested?.Invoke();
        }
    }
}
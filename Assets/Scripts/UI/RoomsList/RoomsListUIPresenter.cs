using System;
using Events;
using UnityEngine;

namespace UI.RoomsList
{
    public class RoomsListUIPresenter
    {
        private readonly RoomsListUIModel _model;
        private readonly RoomsListUIView  _view;
        
        private readonly Action                                       _onLeaveRequested;
        private readonly Action                                       _onJoinRequested;
        private readonly Action                                       _onCreateRoomRequested;
        private readonly Action<string, string, string, bool, string> _onEnterRoomRequested;

        private string _currentLobbyId;

        public RoomsListUIPresenter(
            RoomsListUIModel model,
            RoomsListUIView view,
            Action onLeaveRequested      = null,
            Action onJoinRequested       = null,
            Action onCreateRoomRequested = null,
            Action<string, string, string, bool, string> onEnterRoomRequested = null)
        {
            _model = model;
            _view  = view;
            _onLeaveRequested = onLeaveRequested;
            _onJoinRequested  = onJoinRequested;
            _onCreateRoomRequested = onCreateRoomRequested;
            _onEnterRoomRequested  = onEnterRoomRequested;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnFilterChanged     += ApplyFiltersAndRender;
            _view.OnLeaveClicked      += HandleLeaveClicked;
            _view.OnJoinClicked       += HandleJoinClicked;
            _view.OnCreateRoomClicked += HandleCreateRoomClicked;
            _view.OnRefreshClicked    += HandleRefreshClicked;
            _view.OnEnterRoomClicked  += HandleEnterRoomClicked;
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
            _view.OnJoinClicked       -= HandleJoinClicked;
            _view.OnCreateRoomClicked -= HandleCreateRoomClicked;
            _view.OnRefreshClicked    -= HandleRefreshClicked;
            _view.OnEnterRoomClicked  -= HandleEnterRoomClicked;

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

        private void HandleEnterRoomClicked(RoomInfo room)
        {
            _model.JoinRoom(room.RoomCode);
            _onEnterRoomRequested?.Invoke(room.DisplayName.Value, room.ModeName, room.MapName, room.IsPublic, room.RoomCode);
        }

        private void HandleLeaveClicked() => _onLeaveRequested?.Invoke();

        private void HandleJoinClicked() => _onJoinRequested?.Invoke();

        private void HandleCreateRoomClicked() => _onCreateRoomRequested?.Invoke();
    }
}
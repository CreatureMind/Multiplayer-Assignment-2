using System;
using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;

namespace UI.RoomLobby
{
    public class RoomLobbyUIPresenter
    {
        private readonly RoomLobbyUIModel _model;
        private readonly RoomLobbyUIView _view;

        private readonly string _currentLobbyId;
        private string          _modeName;
        private string          _mapName;

        public RoomLobbyUIPresenter(RoomLobbyUIModel model, RoomLobbyUIView view, string currentLobbyId)
        {
            _model = model;
            _view = view;
            _currentLobbyId = currentLobbyId;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnLeaveClicked += HandleLeaveClicked;
            _view.OnReadyClicked += HandleReadyClicked;
            _view.OnStartClicked += HandleStartClicked;
            _view.OnKickClicked  += HandleKickClicked;
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
            EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
            EventBus.Subscribe<RoomCreatedEvent>      (OnRoomCreated);
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnLeaveClicked -= HandleLeaveClicked;
            _view.OnReadyClicked -= HandleReadyClicked;
            _view.OnStartClicked -= HandleStartClicked;
            _view.OnKickClicked  -= HandleKickClicked;

            EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
            EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
            EventBus.Unsubscribe<RoomCreatedEvent>      (OnRoomCreated);
        }

        public void SetupRoomDetails(string roomName, string modeName, string mapName, bool isPublic, string code)
        {
            _modeName = modeName;
            _mapName = mapName;
            _view.SetHeader(roomName, modeName, mapName);
            _view.SetCodeLabel(isPublic, code);
            RefreshRoomState();
        }

        private void OnRoomCreated(RoomCreatedEvent e)
        {
            SetupRoomDetails(e.RoomName, e.ModeName, e.MapName, e.IsPublic, e.RoomCode);
        }

        private void OnPlayerListChanged(PlayerListChangedEvent e)
        {
            RefreshRoomState();
        }

        private void OnPlayerDataChanged(PlayerDataChangedEvent e)
        {
            RefreshRoomState();
        }

        private void RefreshRoomState()
        {
            // Render list
            var players = _model.GetAllPlayers();
            _view.RenderPlayerList(players, _model.CanKick(), _model.IsLocalPlayer);

            // Notify cross-system chatter
            var nameList = new List<string>();
            foreach (var p in players)
            {
                if (p != null && p.DisplayName.Value != null)
                    nameList.Add(p.DisplayName.Value);
            }
            EventBus.Raise(new OnPlayerListChangedEvent { PlayerNames = nameList });

            // Refresh Ready Button
            var localData = _model.GetLocalPlayerData();
            if (localData != null)
            {
                _view.SetReadyButtonText(localData.IsReady);
            }

            // Refresh Start Button
            var isOwner = _model.CanStartGame();
            var allReady = _model.AreAllPlayersReady();
            _view.SetStartButtonState(isOwner, allReady);
        }

        private async void HandleLeaveClicked()
        {
            try
            {
                _view.SetLeaveButtonEnabled(false);
                await _model.LeaveRoomAsync(_currentLobbyId);
            }
            catch (Exception e)
            {
                EventBus.Raise(new ShowDialogEvent(
                    title: "Failed to leave room",
                    message: "Failed to leave room: " + e.Message,
                    primaryText: "Retry",
                    onPrimary: () => _ = _model.LeaveRoomAsync(_currentLobbyId),
                    secondaryText: "Cancel",
                    onSecondary: null,
                    type: DialogType.Error
                    ));
            }
        }

        private void HandleReadyClicked()
        {
            _model.ToggleReadyState();
        }

        private void HandleStartClicked()
        {
            _model.StartMatch(_modeName, _mapName);
        }

        private void HandleKickClicked(PlayerRef playerRef)
        {
            _model.KickPlayer(playerRef);
        }
    }
}
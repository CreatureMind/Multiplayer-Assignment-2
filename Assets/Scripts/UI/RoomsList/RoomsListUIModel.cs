using System.Collections.Generic;
using System.Linq;
using Events;
using UnityEngine;

namespace UI.RoomsList
{
    public class RoomsListUIModel
    {
        private NetworkManager _networkManager;
        private static RoomListChangedEvent? _cachedRoomData;

        public static RoomListChangedEvent? CachedRoomData => _cachedRoomData;

        public RoomsListUIModel(NetworkManager networkManager = null)
        {
            _networkManager = networkManager ?? NetworkManager.Instance;
        }

        public void UpdateCachedRooms(RoomListChangedEvent roomData)
        {
            _cachedRoomData = roomData;
        }

        public List<RoomInfo> GetFilteredRooms(string roomFilter, string modeFilter, string mapFilter)
        {
            var filteredRooms = new List<RoomInfo>();
            if (!_cachedRoomData.HasValue) return filteredRooms;
            
            var publicRoomsOnly = _cachedRoomData.Value.Rooms.Where(r => r.IsPublic).ToList();

            foreach (var room in publicRoomsOnly)
            {
                var modeName = room.ModeName;
                var mapName = room.MapName;
                var isOpen = (bool)room.IsOpen;

                var matchesRoomFilter = roomFilter == "All" ||
                                        (roomFilter == "Open" && isOpen) ||
                                        (roomFilter == "Closed" && !isOpen);

                var matchesModeFilter = modeFilter == "All" || modeName == modeFilter;
                var matchesMapFilter = mapFilter == "All" || mapName == mapFilter;

                if (matchesRoomFilter && matchesModeFilter && matchesMapFilter)
                {
                    filteredRooms.Add(room);
                }
            }

            return filteredRooms;
        }

        public int GetTotalActivePlayers(int lobbyPlayerCount)
        {
            var manager = _networkManager ? _networkManager : NetworkManager.Instance;
            var additionalCount = manager ? manager.GetAllPlayerCount() : 0;
            return lobbyPlayerCount + additionalCount;
        }

        public void RefreshLobby(string lobbyId)
        {
            var manager = _networkManager ? _networkManager : NetworkManager.Instance;
            if (manager)
            {
                _ = manager.ConnectToCustomLobby(lobbyId);
            }
        }

        public void JoinRoom(string sessionName)
        {
            var manager = _networkManager ? _networkManager : NetworkManager.Instance;
            if (manager)
            {
                _ = manager.JoinRoom(sessionName);
            }
        }
    }
}
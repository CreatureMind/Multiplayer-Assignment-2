using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace UI.RoomLobby
{
    public class RoomLobbyUIModel
    {
        private NetworkManager _networkManager;

        public RoomLobbyUIModel(NetworkManager networkManager = null)
        {
            _networkManager = networkManager ?? NetworkManager.Instance;
        }

        private NetworkManager GetManager() => _networkManager ? _networkManager : NetworkManager.Instance;

        public IEnumerable<PlayerData> GetAllPlayers()
        {
            var manager = GetManager();
            return manager ? manager.GetAllPlayers() : new List<PlayerData>();
        }

        public PlayerData GetLocalPlayerData()
        {
            var manager = GetManager();
            return manager ? manager.GetLocalPlayerData() : null;
        }

        public bool CanStartGame() => GetManager()?.CanStartGame() ?? false;
        public bool AreAllPlayersReady() => GetManager()?.AreAllPlayersReady() ?? false;
        public bool CanKick() => GetManager()?.CanKick() ?? false;
        public bool IsLocalPlayer(PlayerRef playerRef) => GetManager()?.IsLocalPlayer(playerRef) ?? false;

        public void ToggleReadyState()
        {
            var manager = GetManager();
            if (!manager) return;

            var localData = manager.GetLocalPlayerData();
            if (!localData) return;

            manager.SetLocalPlayerReady(!localData.IsReady);
        }

        public async Task LeaveRoomAsync(string currentLobbyId)
        {
            var manager = GetManager();
            if (manager)
            {
                await manager.LeaveRoom(currentLobbyId);
            }
        }

        public void KickPlayer(PlayerRef playerRef)
        {
            GetManager()?.KickPlayer(playerRef);
        }

        public void StartMatch(string modeName, string mapName)
        {
            GetManager()?.StartMatch(modeName, mapName);
        }
    }
}
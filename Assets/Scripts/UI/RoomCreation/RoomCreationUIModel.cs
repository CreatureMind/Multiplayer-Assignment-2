using System.Threading.Tasks;
using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomCreationUIModel
    {
        private NetworkManager _networkManager;

        public RoomCreationUIModel(NetworkManager networkManager = null)
        {
            _networkManager = networkManager ?? NetworkManager.Instance;
        }

        public void CreateRoom(RoomCreationFormData formData, string currentLobbyId)
        {
            if (!_networkManager)
            {
                _networkManager = NetworkManager.Instance;
            }

            if (!_networkManager)
            {
                Debug.LogError("[RoomCreationUIModel] NetworkManager instance missing!");
                return;
            }

            Debug.Log($"Creating room with name: {formData.RoomName}, max players: {formData.MaxPlayers}, mode: {formData.SelectedMode}, map: {formData.SelectedMap}, isPublic: {formData.IsPublic}");

            _ = _networkManager.CreateRoomInCurrentLobby(
                formData.RoomName,
                formData.MaxPlayers,
                currentLobbyId,
                formData.SelectedMode,
                formData.SelectedMap,
                formData.IsPublic
            );
        }
    }
}
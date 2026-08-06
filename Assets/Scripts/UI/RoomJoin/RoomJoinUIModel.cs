using System.Threading.Tasks;
using UnityEngine;

namespace UI.RoomCreation
{
    public class RoomJoinUIModel
    {
        private NetworkManager _networkManager;

        public RoomJoinUIModel(NetworkManager networkManager = null)
        {
            _networkManager = networkManager ?? NetworkManager.Instance;
        }

        public void JoinRoom(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName)) return;
            
            var manager = _networkManager ? _networkManager : NetworkManager.Instance;
            if (manager)
            {
                _ = manager.JoinRoom(sessionName);
            }
        }
    }
}
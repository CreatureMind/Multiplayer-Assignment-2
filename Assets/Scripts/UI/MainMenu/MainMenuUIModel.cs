using UnityEngine;

namespace UI.MainMenu
{
    public class MainMenuUIModel
    {
        private NetworkManager _networkManager;

        public MainMenuUIModel(NetworkManager networkManager = null)
        {
            _networkManager = networkManager ?? NetworkManager.Instance;
        }

        public void EnterGlobalLobby()
        {
            var manager = _networkManager ? _networkManager : NetworkManager.Instance;
            if (!manager)
            {
                Debug.LogError("[MainMenuUIModel] NetworkManager instance is missing!");
                return;
            }

            // Connects to the server Lobby Hub (or re-surfaces the cached list if already connected)
            _ = manager.ConnectToCustomLobby();
        }
    }
}
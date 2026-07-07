using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public class MasterClientObjectSpawner : MonoBehaviour
{
    [SerializeField] private ReadyManager readyManagerPrefab;
    [SerializeField] private ChatRelay chatRelayPrefab;
    [SerializeField] private CharacterSelectionManager characterSelectionManagerPrefab;

    private const string GAME_SCENE = "Game_Scene";
    
    public void EnsureLobbyObjects(NetworkRunner runner)
    {
        EnsureReadyManager(runner);
        EnsureChatRelay(runner);
    }
    
    public void EnsureGameObjects(NetworkRunner runner)
    {
        EnsureCharacterSelectionManager(runner);
        
        if (IsGameScene())
            EnsureChatRelay(runner);
    }
    
    private void EnsureReadyManager(NetworkRunner runner)
    {
        if (!runner.IsSharedModeMasterClient)
            return;
        if (NetworkManager.Instance && NetworkManager.Instance.ReadyManagerInstance)
            return;

        runner.Spawn(readyManagerPrefab);
    }

    private void EnsureChatRelay(NetworkRunner runner)
    {
        if (!runner.IsSharedModeMasterClient)
            return;

        var chat = NetworkManager.Instance ? NetworkManager.Instance.ChatNetworkManager : null;
        if (chat && chat.ChatRelay)
            return;

        var relay = runner.Spawn(chatRelayPrefab);
        if (relay)
            relay.name = $"ChatRelay({runner.LocalPlayer.ToString()})";
    }
    
    private void EnsureCharacterSelectionManager(NetworkRunner runner)
    {
        if (!runner.IsSharedModeMasterClient)
            return;
        if (CharacterSelectionManager.Instance)
            return;

        runner.Spawn(characterSelectionManagerPrefab);
    }

    private static bool IsGameScene()
        => SceneManager.GetActiveScene().name.Contains(GAME_SCENE);
}
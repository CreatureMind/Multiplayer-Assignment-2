using UnityEngine;
using Fusion;

public class InGameBootstrapper : MonoBehaviour
{
    [SerializeField] private ServerGameManager serverGameManagerPrefab;
    
    private ServerGameManager _serverGameInstance;

    private void Awake()
    {
#if UNITY_SERVER
        _serverGameInstance = FindAnyObjectByType<NetworkRunner>().Spawn(serverGameManagerPrefab);
#endif
    }
}
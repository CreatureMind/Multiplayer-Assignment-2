using System.Collections;
using UnityEngine;

public class GameLogicBoot : MonoBehaviour
{
    [SerializeField] private float _pollIntervalSeconds = 0.2f;
    [SerializeField] private float _statusLogEverySeconds = 5f;

    private bool _requestSent;

    private void Awake()
    {
#if !UNITY_SERVER
        Destroy(gameObject);
#endif
    }

    private void Start()
    {
#if UNITY_SERVER
        StartCoroutine(FindServerGameManagerAndRequestRoutine());
#endif
    }

    private IEnumerator FindServerGameManagerAndRequestRoutine()
    {
        Debug.Log($"[GameLogicBoot] Server polling started in scene '{gameObject.scene.name}'. Waiting for spawned ServerGameManager...");

        var elapsedSinceStatusLog = 0f;

        while (!_requestSent)
        {
            if (TryFindSpawnedServerGameManager(out var serverGameManager))
            {
                _requestSent = true;
                Debug.Log($"[GameLogicBoot] Found ServerGameManager '{serverGameManager.name}' in scene '{serverGameManager.gameObject.scene.name}'. Dispatching RequestInstantiation().");
                serverGameManager.RequestInstantiation();
                Debug.Log("[GameLogicBoot] RequestInstantiation dispatched successfully.");
                Destroy(gameObject);
                yield break;
            }

            elapsedSinceStatusLog += _pollIntervalSeconds;
            if (elapsedSinceStatusLog >= _statusLogEverySeconds)
            {
                elapsedSinceStatusLog = 0f;
                Debug.Log("[GameLogicBoot] Still searching for spawned ServerGameManager...");
            }

            yield return new WaitForSeconds(_pollIntervalSeconds);
        }
    }

    private bool TryFindSpawnedServerGameManager(out ServerGameManager serverGameManager)
    {
        serverGameManager = null;

        var managers = FindObjectsByType<ServerGameManager>();
        if (managers == null || managers.Length == 0)
            return false;

        foreach (var manager in managers)
        {
            if (!manager || !manager.isActiveAndEnabled)
                continue;

            serverGameManager = manager;
            return true;
        }

        return false;
    }
}

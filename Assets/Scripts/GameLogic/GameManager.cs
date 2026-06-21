using System;
using Events;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private CubeSpawner cubeSpawnerPrefab;
    
    private bool _cubeSpawnerSpawned;

    private void Awake()
    {
        EventBus.Subscribe<CharacterSelectionConfirmedEvent>(SpawnCubeSpawner);
    }
    

    private void SpawnCubeSpawner(CharacterSelectionConfirmedEvent e)
    {
        if (_cubeSpawnerSpawned)
            return;
        
        var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
        if (!runner)
        {
            Debug.LogError("No NetworkRunner found in the scene. Please ensure a NetworkRunner is present.");
            return;
        }
        
        runner.Spawn(cubeSpawnerPrefab, Vector3.zero, Quaternion.identity);
        _cubeSpawnerSpawned = true;
    }

    
    private void OnDestroy()
    {
        EventBus.Unsubscribe<CharacterSelectionConfirmedEvent>(SpawnCubeSpawner);
    }
}

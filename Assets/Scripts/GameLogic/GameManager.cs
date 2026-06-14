using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private CubeSpawner cubeSpawnerPrefab;
    private NetworkRunner _networkRunnerInstance;

    private void Awake()
    {
        _networkRunnerInstance = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
        if (_networkRunnerInstance == null)
        {
            Debug.LogError("No NetworkRunner found in the scene. Please ensure a NetworkRunner is present.");
        }
        
        _networkRunnerInstance.Spawn(cubeSpawnerPrefab, Vector3.zero, Quaternion.identity);
    }
}

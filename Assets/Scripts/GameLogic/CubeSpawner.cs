using Fusion;
using UnityEngine;

public class CubeSpawner : NetworkBehaviour
{
    [SerializeField] private CubeMaterialChanger cubePrefab;

    private PlayerInputHandler inputHandler;

    public override void Spawned()
    {
        inputHandler = PlayerInputHandler.Instance;
        if (inputHandler != null) inputHandler.OnMouseInput += HandleMouseInput;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (inputHandler != null) inputHandler.OnMouseInput -= HandleMouseInput;
    }

    private void HandleMouseInput(InputType type, bool performed, Vector2 pos)
    {
        if (type != InputType.LMB || !performed) return;
        if (Camera.main == null || Runner == null) return;

        if (cubePrefab == null)
        {
            Debug.LogWarning("CubeSpawner: cubePrefab not assigned or not registered as spawnable.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(pos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        if (hit.collider.CompareTag("Floor"))
        {
            if (!HasStateAuthority) return;
            SpawnCube(hit.point);
        }
        else if (hit.collider.CompareTag("Cube"))
            DestroyCube(hit.collider);
    }

    private void SpawnCube(Vector3 spawnPos)
    {
        var playerData = NetworkManager.Instance.GetLocalPlayerData();
        if (playerData == null)
        {
            Debug.LogWarning("CubeSpawner: playerData is null");
            return;
        }
        var cube = Runner.Spawn(cubePrefab, spawnPos, Quaternion.identity, inputAuthority: Runner.LocalPlayer);
        cube.InstantiateMaterialColor(playerData.CharacterColor);
    }

    private void DestroyCube(Collider cubeCollider)
    {
        var cmc = cubeCollider.GetComponent<CubeMaterialChanger>();
        if (cmc == null) return;
        cmc.RequestDestroy();
    }
}
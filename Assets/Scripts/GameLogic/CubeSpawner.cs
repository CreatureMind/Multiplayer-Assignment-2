using Fusion;
using UnityEngine;

public class CubeSpawner : NetworkBehaviour
{
    [SerializeField] private CubeMaterialChanger cubePrefab;

    private PlayerInputHandler inputHandler;


    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        inputHandler = PlayerInputHandler.Instance;
        if (inputHandler != null) inputHandler.OnMouseInput += HandleMouseInput;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (inputHandler != null) inputHandler.OnMouseInput -= HandleMouseInput;
    }

    private void HandleMouseInput(InputType type, bool performed, Vector2 pos)
    {
        if (!HasStateAuthority) return;
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
            Vector3 spawnPos = hit.point;
            // Spawn with ownership of this player (Object.InputAuthority) and optional init callback
            var cube = Runner.Spawn(cubePrefab, spawnPos, Quaternion.identity, Object.InputAuthority);
            cube.InstantiateMaterialColor(Random.ColorHSV());
        }

        if (hit.collider.CompareTag("Cube"))
        {
            var cmc = hit.collider.GetComponent<CubeMaterialChanger>().Object;
            
            Runner.Despawn(cmc);
        }
    }
}
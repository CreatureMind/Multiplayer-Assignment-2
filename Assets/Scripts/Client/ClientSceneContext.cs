using UnityEngine;

// Scene-side rendering rig. Exactly one per client scene.
// A spawned ClientManager cannot hold references to scene objects, so the local-authority instance pulls its rig from here instead.
public sealed class ClientSceneContext : MonoBehaviour
{
    public static ClientSceneContext Instance { get; private set; }
    
    [Header("Rendering rig")]
    [SerializeField] private Grid grid;
    [SerializeField] private Camera boardCamera;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private BoardView boardView;
    
    [Header("Board Placement")]
    [SerializeField] private Vector3Int boardOriginCell = Vector3Int.zero;
    
    public Grid Grid => grid;
    public Camera BoardCamera => boardCamera;
    public InputHandler InputHandler => inputHandler;
    public IBoardRenderer Renderer => boardView;
    public Vector3Int BoardOriginCell => boardOriginCell;
    
    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("[ClientSceneContext] More than one in the scene; destroying the duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fail loud at startup rather than NRE-ing deep in ClientManager wiring.
        if (!grid || !boardCamera || !inputHandler || !boardView)
            Debug.LogError("[ClientSceneContext] One or more rig references are unassigned.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
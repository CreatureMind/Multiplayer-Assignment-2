using UnityEngine;

public sealed class ClientBoardPreview : MonoBehaviour
{
    [Header("What to preview")]
    [SerializeField] private StartingPositionSO startingPosition;
    [SerializeField, Range(1, 4)] private int viewerId = 1;

    [Header("Options")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool previewLegalMoveHighlights;
    
    private ClientBoardCache _board;
    private BoardCoordinateMapper _mapper;

    private void Start()
    {
        if (rebuildOnStart)
            BuildPreview();
    }
    
    [ContextMenu("Rebuild Preview")]
    public void BuildPreview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ClientBoardPreview] Enter play mode: this needs a live camera and tilemaps.");
            return;
        }

        var context = ClientSceneContext.Instance;
        if (!context)
        {
            Debug.LogError("[ClientBoardPreview] No ClientSceneContext in the scene.");
            return;
        }
        if (!startingPosition)
        {
            Debug.LogError("[ClientBoardPreview] No StartingPositionSO assigned.");
            return;
        }
        if (!context.Grid || !context.BoardCamera || context.Renderer == null)
        {
            Debug.LogError("[ClientBoardPreview] ClientSceneContext rig (Grid / Camera / BoardView) is incomplete.");
            return;
        }

        var localId = (byte)viewerId;

        // 1. Build the exact objects RPC_InitialiseClient builds.
        _mapper = new BoardCoordinateMapper(
            context.Grid, context.BoardCamera, context.BoardOriginCell,
            startingPosition.Width, startingPosition.Height);
        _board = new ClientBoardCache(startingPosition.Width, startingPosition.Height);

        // 2. Wire the renderer to the fresh cache (subscribes to Changed, clears the tilemap).
        context.Renderer.Initialise(_board, _mapper, localId);

        // 3. Project the whole authored board for this viewer and apply it in one batch.
        //    Apply raises Changed once -> BoardView paints. Same downstream path as the live diff stream.
        var diffs = StartingPositionProjector.BuildDiffs(startingPosition, localId);
        _board.Apply(diffs);

        // 4. Overlays. Clear hover; optionally compute + show legal-move highlights for this viewer.
        context.Renderer.SetHover(null);
        if (previewLegalMoveHighlights)
        {
            var legal = new LegalMoveCalculator(_board, localId);
            legal.Recompute();
            context.Renderer.SetHighlights(legal.MoveTargets);
        }
        else
        {
            context.Renderer.SetHighlights(null);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Renders the board. Purely reactive: it subscribes to ClientBoardCache.Changed and paints the diffs it is handed.
public class BoardView : MonoBehaviour, IBoardRenderer
{
    [Header("Tilemaps (all under the mapper's Grid)")]
    [SerializeField] private Tilemap baseTilemap;
    [SerializeField] private Tilemap highlightTilemap;
    [SerializeField] private Tilemap hoverTilemap;
    [SerializeField, Min(0f)] private float hoverFadeSeconds = 0.25f;
    
    [Header("Assets")]
    [SerializeField] private TileVisualCatalogSO catalog;
    [SerializeField] private TileBase overlayTile;
    [SerializeField] private Color highlightColor = new(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverColor = new(1f, 0.95f, 0.4f, 0.5f);
    
    [Header("Blast Animation")]
    [SerializeField] private bool animateBlasts = true;
    [SerializeField, Min(0f)] private float blastStepSeconds = 0.06f;

    private ClientBoardCache _board;
    private BoardCoordinateMapper _mapper;
    private byte _localPlayerId;
    
    // Track what we set on the overlays so we clear only those cells, not the whole tilemap.
    private readonly List<Vector2Int> _activeHighlights = new();
    private HoverTrail _hoverTrail;

    private Coroutine _blastRoutine;
    private IReadOnlyList<CellDiff> _runningBlast;

    public void Initialise(ClientBoardCache board, BoardCoordinateMapper mapper, byte localPlayerId)
    {
        _board = board;
        _mapper = mapper;
        _hoverTrail = new HoverTrail(hoverTilemap, _mapper, overlayTile, hoverColor, hoverFadeSeconds);
        _localPlayerId = localPlayerId;

        if (_board != null)
        {
            _board.Changed += OnBoardChanged;
        }

        RepaintAll(); // the cache may already hold data received before this call
    }
    
    private void Update() => _hoverTrail?.Tick(Time.deltaTime);
    
    public void SetHighlights(IReadOnlyCollection<Vector2Int> cells)
    {
        if (!highlightTilemap)
            return;

        foreach (var tile in _activeHighlights)
            highlightTilemap.SetTile(_mapper.BoardToCell(tile), null);

        _activeHighlights.Clear();

        if (cells == null)
            return;

        foreach (var boardCell in cells)
        {
            if (_board != null && !_board.Contains(boardCell))
                continue;
            PaintOverlay(highlightTilemap, boardCell, highlightColor);
            _activeHighlights.Add(boardCell);
        }
    }
    
    public void SetHover(Vector2Int? boardCell)
    {
        if (boardCell.HasValue && _board != null && !_board.Contains(boardCell.Value))
            boardCell = null;
        _hoverTrail?.SetCurrent(boardCell);
    }

    private void OnDestroy()
    {
        if (_board != null)
            _board.Changed -= OnBoardChanged;
        if (_blastRoutine != null)
            StopCoroutine(_blastRoutine);
        _hoverTrail?.Clear();
    }
    
    private void RepaintAll()
    {
        if (_board == null || !baseTilemap)
            return;

        baseTilemap.ClearAllTiles();
        for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                PaintCell(cell, _board[cell]);
            }
    }
    
    private void OnBoardChanged(IReadOnlyList<CellDiff> diffs)
    {
        if (diffs == null || diffs.Count == 0)
            return;

        // Snap any in-flight blast to its final state so no cell is left mid-animation.
        // In round-robin play this effectively never fires, but it keeps the visual honest.
        if (_blastRoutine != null)
        {
            StopCoroutine(_blastRoutine);
            _blastRoutine = null;
            if (_runningBlast != null)
                PaintAll(_runningBlast);
            _runningBlast = null;
        }

        if (animateBlasts && blastStepSeconds > 0f && HasBlastWave(diffs))
        {
            _runningBlast = diffs;
            _blastRoutine = StartCoroutine(AnimateBatch(diffs));
        }
        else
        {
            PaintAll(diffs);
        }
    }
    
    // Applies generation 0 immediately, then each wave after blastStepSeconds
    // The cascade spreads outward using the Generation index the server tagged each cleared cell with.
    private IEnumerator AnimateBatch(IReadOnlyList<CellDiff> diffs)
    {
        byte maxGen = 0;
        for (var i = 0; i < diffs.Count; i++)
            if (diffs[i].Generation > maxGen)
                maxGen = diffs[i].Generation;

        for (var gen = 0; gen <= maxGen; gen++)
        {
            var paintedAny = false;
            for (var i = 0; i < diffs.Count; i++)
            {
                if (diffs[i].Generation != gen)
                    continue;
                PaintDiff(diffs[i]);
                paintedAny = true;
            }

            if (gen < maxGen && paintedAny)
                yield return new WaitForSeconds(blastStepSeconds);
        }

        _runningBlast = null;
        _blastRoutine = null;
    }
    
    private static bool HasBlastWave(IReadOnlyList<CellDiff> diffs)
    {
        for (var i = 0; i < diffs.Count; i++)
            if (diffs[i].Generation > 0)
                return true;
        return false;
    }

    private void PaintAll(IReadOnlyList<CellDiff> diffs)
    {
        foreach (var diff in diffs)
            PaintDiff(diff);
    }
    
    private void PaintDiff(in CellDiff diff) => PaintCell(diff.Cell, diff.ToView());
    
    private void PaintCell(Vector2Int boardCell, in TileView view)
    {
        if (_board == null || !baseTilemap || !_board.Contains(boardCell))
            return;

        var cell = _mapper.BoardToCell(boardCell);

        // None means "no diff received yet" - distinct from Empty, which is real state.
        if (view.VisualType == TileType.None)
        {
            baseTilemap.SetTile(cell, null);
            return;
        }

        var tile = catalog ? catalog.GetTile(view.VisualType) : null;
        baseTilemap.SetTile(cell, tile);

        if (tile)
        {
            // Tiles default to TileFlags.LockColor, which makes SetColor silently do nothing.
            // Clear the flag AFTER SetTile, then color.
            baseTilemap.SetTileFlags(cell, TileFlags.None);
            baseTilemap.SetColor(cell, catalog.GetColor(view));
        }
    }

    private void PaintOverlay(Tilemap map, Vector2Int boardCell, Color color)
    {
        var cell = _mapper.BoardToCell(boardCell);
        map.SetTile(cell, overlayTile);
        map.SetTileFlags(cell, TileFlags.None);
        map.SetColor(cell, color);
    }
}
using System.Collections.Generic;
using UnityEngine;

// Finds every 4x4 window the local player could turn into a base.
// Rules: all 16 cells are mine and unfrozen; the middle 2x2 must be plain soldiers; the outer 12 may be soldiers OR bombs.
// Cost is 0(cells * 16) - about 40k reads on a 50x50 board. Fine on turn start or on entering build mode. Do not call it per frame.
public sealed class BaseFormationScanner
{
    private const int WindowSize = 4;
    private const int CoreOffset = 1; // middle 2x2 starts on cell in
    private const int CoreSize = 2;

    private readonly ClientBoardCache _board;
    private readonly byte _localPlayerId;
    
    private readonly List<Vector2Int> _origins = new List<Vector2Int>();
    
    // Maps each cell of a candidate's middle 2x2 back to that candidate's origin,
    // so a click on the visible highlight resolves to a window.
    // First writer wins where windows overlap - see note below.
    private readonly Dictionary<Vector2Int, Vector2Int> _coreToOrigin = new Dictionary<Vector2Int, Vector2Int>();
    
    public IReadOnlyList<Vector2Int> Origins => _origins;
    public IReadOnlyCollection<Vector2Int> HighlightCells => _coreToOrigin.Keys;

    public BaseFormationScanner(ClientBoardCache board, byte localPlayerId)
    {
        _board = board;
        _localPlayerId = localPlayerId;
    }
    
    public bool TryGetOriginForCell(Vector2Int cell, out Vector2Int origin)
        => _coreToOrigin.TryGetValue(cell, out origin);

    public void Recompute()
    {
        _origins.Clear();
        _coreToOrigin.Clear();
        
        var maxX = _board.Width - WindowSize;
        var maxY = _board.Height - WindowSize;
        
        for (var oy = 0; oy <= maxY; oy++)
            for (var ox = 0; ox <= maxX; ox++)
            {
                var origin = new Vector2Int(ox, oy);
                if (!IsValidWindow(origin))
                    continue;
                
                _origins.Add(origin);
                
                for (var dy = 0; dy < CoreSize; dy++)
                    for (var dx = 0; dx < CoreSize; dx++)
                    {
                        var coreCell = new Vector2Int(ox + CoreOffset + dx, oy + CoreOffset + dy);
                        // Overlapping windows: keep the first. The player can still pick the other by clicking a core cell unique to it.
                        // If that proves fiddly in play, switch to a click-to-cycle UI.
                        _coreToOrigin.TryAdd(coreCell, origin);
                    }
            }
    }

    private bool IsValidWindow(Vector2Int origin)
    {
        for (var dy = 0; dy < WindowSize; dy++)
            for (var dx = 0; dx < WindowSize; dx++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                var view = _board[cell]; // bounds guaranteed by the caller's loop

                if (view.OwnerId != _localPlayerId)
                    return false;
                if (view.Frozen)
                    return false;
                
                var isCore = dx is >= CoreOffset and < CoreOffset + CoreSize
                             && dy is >= CoreOffset and < CoreOffset + CoreSize;
                
                if (isCore)
                    if (view.VisualType != TileType.Pawn)
                        return false;
                else
                    if (view.VisualType != TileType.Pawn && view.VisualType != TileType.Bomb)
                        return false;
            }
        return true;
    }
}
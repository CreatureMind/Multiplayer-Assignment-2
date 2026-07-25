using System;
using System.Collections.Generic;
using UnityEngine;

// The client's local mirror of the board, built entirely from server diffs.
// NOT authoritative and NOT a secret: the server has already projected enemy bombs down to soldiers before sending,
// so this array genuinely does not contain hidden information even in memory.
// Observer: renderers and the legal-move calculator subscribe to Changed rather than polling
public sealed class ClientBoardCache
{
    public int Width { get; }
    public int Height { get; }

    private readonly TileView[] _tiles; // flat; index = y * Width + x
    
    // Fires once per applied batch with the cells that changed, so subscribers can update only those cells
    public event Action<IReadOnlyList<CellDiff>> Changed;

    public ClientBoardCache(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");
        
        Width  = width;
        Height = height;
        _tiles = new TileView[width * height];
    }
    
    public bool Contains(Vector2Int cell)
        => cell is { x: >= 0, y: >= 0 } && cell.x < Width && cell.y < Height;
    
    // Unchecked indexer for hot loops that have already bounds-checked
    public TileView this[Vector2Int cell] => _tiles[cell.y * Width + cell.x];

    public bool TryGet(Vector2Int cell, out TileView view)
    {
        if (!Contains(cell))
        {
            view = default;
            return false;
        }
        
        view = _tiles[cell.y * Width + cell.x];
        return true;
    }

    // Applies a server diff batch. Out-of-range cells are logged and dropped rather than thrown on - a malformed packet should not kill the client
    public void Apply(IReadOnlyList<CellDiff> diffs)
    {
        if (diffs == null || diffs.Count == 0)
            return;

        for (var i = 0; i < diffs.Count; i++)
        {
            var cell = diffs[i].Cell;
            if (!Contains(cell))
            {
                Debug.LogWarning($"[ClientBoardCache] Diff for out-of-range cell {cell} dropped.");
                continue;
            }
            
            _tiles[cell.y * Width + cell.x] = diffs[i].ToView();
        }

        Changed?.Invoke(diffs);
    }
}
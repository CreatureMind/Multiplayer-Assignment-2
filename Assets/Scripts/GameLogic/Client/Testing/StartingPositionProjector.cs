using System.Collections.Generic;
using UnityEngine;

public static class StartingPositionProjector
{
    public static List<CellDiff> BuildDiffs(IReadOnlyList<TileState> tiles, int width, int height, byte viewerId)
    {
        var diffs = new List<CellDiff>();
        if (width <= 0 || height <= 0)
        {
            Debug.LogError($"[StartingPositionProjector] Non-positive dimensions {width}x{height}.");
            return diffs;
        }
        if (tiles == null || tiles.Count != width * height)
        {
            Debug.LogError($"[StartingPositionProjector] Tile count {(tiles?.Count ?? 0)} != {width}x{height}.");
            return diffs;
        }

        diffs.Capacity = tiles.Count;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var state = tiles[y * width + x];
                var view = TileProjector.Project(state, viewerId, frozen: false);
                diffs.Add(CellDiff.From(new Vector2Int(x, y), view.VisualType, view.OwnerId, view.Frozen));
            }
        return diffs;
    }
    
    public static List<CellDiff> BuildDiffs(StartingPositionSO so, byte viewerId)
    {
        if (!so)
        {
            Debug.LogError("[StartingPositionProjector] StartingPositionSO is null.");
            return new List<CellDiff>();
        }
        return BuildDiffs(so.BuildTileStates(), so.Width, so.Height, viewerId);
    }
}
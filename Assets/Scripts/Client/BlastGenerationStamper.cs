using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlastGenerationStamper
{
    private static readonly Vector2Int[] Neighbours8 =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };
    
    private readonly ClientBoardCache _board;

    public BlastGenerationStamper(ClientBoardCache board) => _board = board;
    
    public void Stamp(List<CellDiff> diffs)
    {
        if (diffs == null || diffs.Count == 0)
            return;
        
        var blastCells = new List<Vector2Int>();
        var indexByCell = new Dictionary<Vector2Int, int>(diffs.Count);
        for (var i = 0; i < diffs.Count; i++)
        {
            var diff = diffs[i];
            indexByCell[diff.Cell] = i;

            if ((TileType)diff.VisualType != TileType.Empty)
                continue;
            if (!_board.TryGet(diff.Cell, out var old) || old.VisualType == TileType.Empty)
                continue;

            blastCells.Add(diff.Cell);
        }

        if (blastCells.Count == 0)
            return;
        
        var seed = NearestToCentroid(blastCells);
        
        var blastSet = new HashSet<Vector2Int>(blastCells);
        var generationByCell = new Dictionary<Vector2Int, byte> { [seed] = 1 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var nextGen = (byte)Mathf.Min(generationByCell[cell] + 1, byte.MaxValue);

            foreach (var offset in Neighbours8)
            {
                var n = cell + offset;
                if (!blastSet.Contains(n) || !generationByCell.TryAdd(n, nextGen))
                    continue;
                queue.Enqueue(n);
            }
        }
        
        foreach (var cell in blastCells)
            generationByCell.TryAdd(cell, 1);
        
        foreach (var kvp in generationByCell)
        {
            var i = indexByCell[kvp.Key];
            var diff = diffs[i];
            diff.Generation = kvp.Value;
            diffs[i] = diff;
        }
    }
    
    private static Vector2Int NearestToCentroid(List<Vector2Int> cells)
    {
        var sum = cells.Aggregate(Vector2.zero, (current, c) => current + new Vector2(c.x, c.y));
        var centroid = sum / cells.Count;

        var best = cells[0];
        var bestSqr = float.MaxValue;
        foreach (var c in cells)
        {
            var d = (new Vector2(c.x, c.y) - centroid).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = c;
            }
        }
        return best;
    }
}
using System.Collections.Generic;
using UnityEngine;

// Server-side read-only board queries used to validate requests. Owner ids are 1-based (0 = no owner).
public static class ServerBoardRules
{
    private static readonly Vector2Int[] Orthogonal =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    
    // True if `target` is reachable to a friendly Base/Motherload through the player's own 4-connected chain.
    public static bool ConnectsToBase(BoardManager board, byte ownerId, Vector2Int target)
    {
        if (ownerId == TileState.NoOwner)
            return false;

        var visited = new HashSet<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        foreach (var dir in Orthogonal) // seed from friendly conductors adjacent to the (empty) target
        {
            var n = target + dir;
            if (IsFriendlyConductor(board, ownerId, n) && visited.Add(n))
                stack.Push(n);
        }

        while (stack.Count > 0)
        {
            var cell = stack.Pop();
            if (!board.TryGetTile(cell.x, cell.y, out var tile))
                continue;
            if (tile.OwnerId == ownerId && tile.IsTerritory)
                return true; // reached my base/motherload
            foreach (var dir in Orthogonal)
            {
                var n = cell + dir;
                if (IsFriendlyConductor(board, ownerId, n) && visited.Add(n))
                    stack.Push(n);
            }
        }
        
        return false;
    }
    
    // All 16 mine; outer 12 soldier-or-bomb; inner 2x2 plain soldiers. Mirrors BaseFormationScanner.
    public static bool IsBaseWindow(BoardManager board, byte ownerId, Vector2Int origin)
    {
        for (var dy = 0; dy < 4; dy++)
            for (var dx = 0; dx < 4; dx++)
            {
                if (!board.TryGetTile(origin.x + dx, origin.y + dy, out var t))
                    return false;
                if (t.OwnerId != ownerId)
                    return false;
                var isCore = dx is 1 or 2 && dy is 1 or 2;
                if (isCore) { if (t.Type != TileType.Soldier)
                    return false; }
                else if (t.Type != TileType.Soldier && t.Type != TileType.Bomb)
                    return false;
            }
        
        return true;
    }
    
    private static bool IsFriendlyConductor(BoardManager board, byte ownerId, Vector2Int cell)
        => board.TryGetTile(cell.x, cell.y, out var t) && t.OwnerId == ownerId && t.ConductsConnectivity;
}
using System.Collections.Generic;
using UnityEngine;

// Server-side read-only board queries used to validate requests. Owner ids are 1-based (0 = no owner).
public static class ServerBoardRules
{
    private static readonly Vector2Int[] Orthogonal =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private static readonly Vector2Int[] Neighbors8 =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        new Vector2Int(-1, 0),                           new Vector2Int(1, 0),
        new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
    };
    
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
    
    public static bool ConqueredBasesByPawnPlacementCheck(BoardManager board, byte ownerId, Vector2Int origin, out HashSet<Vector2Int> conqueredBases)
    {
        conqueredBases = new HashSet<Vector2Int>();
        if (!board || ownerId == TileState.NoOwner)
            return false;

        var candidateBottomLefts = new HashSet<Vector2Int>();
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!board.TryGetTile(cell.x, cell.y, out var tile) || tile.Type != TileType.Base)
                    continue;

                var candidates = new[]
                {
                    cell,
                    new Vector2Int(cell.x - 1, cell.y),
                    new Vector2Int(cell.x, cell.y - 1),
                    new Vector2Int(cell.x - 1, cell.y - 1)
                };

                foreach (var bottomLeft in candidates)
                    if (IsTwoByTwoBase(board, bottomLeft))
                        candidateBottomLefts.Add(bottomLeft);
            }

        foreach (var bottomLeft in candidateBottomLefts)
            if (IsBaseOwnedByOtherPlayer(board, bottomLeft, ownerId) &&
                IsBaseRingFullyOwnedByPlayerFormation(board, bottomLeft, ownerId))
                conqueredBases.Add(bottomLeft);

        return conqueredBases.Count > 0;
    }

    public static bool MotherloadConqueredWinConditionCheck(BoardManager board, byte ownerId, Vector2Int origin)
    {
        if (!board || ownerId == TileState.NoOwner)
            return false;

        var motherloadSeeds = new HashSet<Vector2Int>();
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (board.TryGetTile(cell.x, cell.y, out var tile) && tile.Type == TileType.Motherload)
                    motherloadSeeds.Add(cell);
            }

        if (motherloadSeeds.Count == 0)
            return false;

        var motherloadArea = BuildMotherloadArea(board, motherloadSeeds);
        if (motherloadArea.Count == 0)
            return false;

        var boundary = new HashSet<Vector2Int>();
        foreach (var motherloadCell in motherloadArea)
            foreach (var dir in Neighbors8)
            {
                var around = motherloadCell + dir;
                if (!motherloadArea.Contains(around))
                    boundary.Add(around);
            }

        if (boundary.Count == 0)
            return false;

        foreach (var boundaryCell in boundary)
        {
            if (!board.TryGetTile(boundaryCell.x, boundaryCell.y, out var tile))
                return false;

            var surroundedByPlayer = tile.OwnerId == ownerId &&
                                     (tile.Type == TileType.Soldier || tile.Type == TileType.Bomb);
            if (!surroundedByPlayer)
                return false;
        }

        return true;
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

    private static bool IsTwoByTwoBase(BoardManager board, Vector2Int bottomLeft)
    {
        return IsBaseTile(board, bottomLeft) &&
               IsBaseTile(board, new Vector2Int(bottomLeft.x + 1, bottomLeft.y)) &&
               IsBaseTile(board, new Vector2Int(bottomLeft.x, bottomLeft.y + 1)) &&
               IsBaseTile(board, new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1));
    }

    private static bool IsBaseTile(BoardManager board, Vector2Int cell)
        => board.TryGetTile(cell.x, cell.y, out var tile) && tile.Type == TileType.Base;

    private static bool IsBaseOwnedByOtherPlayer(BoardManager board, Vector2Int bottomLeft, byte ownerId)
    {
        var allOwnedByPlayer = true;
        var baseTiles = new[]
        {
            bottomLeft,
            new Vector2Int(bottomLeft.x + 1, bottomLeft.y),
            new Vector2Int(bottomLeft.x, bottomLeft.y + 1),
            new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1)
        };

        foreach (var cell in baseTiles)
        {
            if (!board.TryGetTile(cell.x, cell.y, out var tile) || tile.Type != TileType.Base)
                return false;

            if (tile.OwnerId != ownerId)
                allOwnedByPlayer = false;
        }

        return !allOwnedByPlayer;
    }

    private static bool IsBaseRingFullyOwnedByPlayerFormation(BoardManager board, Vector2Int bottomLeft, byte ownerId)
    {
        for (var y = bottomLeft.y - 1; y <= bottomLeft.y + 2; y++)
            for (var x = bottomLeft.x - 1; x <= bottomLeft.x + 2; x++)
            {
                var isInsideBase = x >= bottomLeft.x && x <= bottomLeft.x + 1 &&
                                   y >= bottomLeft.y && y <= bottomLeft.y + 1;
                if (isInsideBase)
                    continue;

                if (!board.TryGetTile(x, y, out var tile))
                    return false;

                var surroundedByPlayer = tile.OwnerId == ownerId &&
                                         (tile.Type == TileType.Soldier || tile.Type == TileType.Bomb);
                if (!surroundedByPlayer)
                    return false;
            }

        return true;
    }

    private static HashSet<Vector2Int> BuildMotherloadArea(BoardManager board, HashSet<Vector2Int> seeds)
    {
        var area = new HashSet<Vector2Int>();
        if (seeds.Count == 0)
            return area;

        var territoryId = TileState.NoTerritory;
        foreach (var seed in seeds)
        {
            if (board.TryGetTile(seed.x, seed.y, out var tile) && tile.Type == TileType.Motherload)
            {
                territoryId = tile.TerritoryId;
                break;
            }
        }

        if (territoryId != TileState.NoTerritory)
        {
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                    if (board.TryGetTile(x, y, out var tile) &&
                        tile.Type == TileType.Motherload &&
                        tile.TerritoryId == territoryId)
                        area.Add(new Vector2Int(x, y));
            return area;
        }

        var stack = new Stack<Vector2Int>();
        foreach (var seed in seeds)
            if (area.Add(seed))
                stack.Push(seed);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var dir in Orthogonal)
            {
                var next = current + dir;
                if (area.Contains(next))
                    continue;
                if (!board.TryGetTile(next.x, next.y, out var tile) || tile.Type != TileType.Motherload)
                    continue;
                area.Add(next);
                stack.Push(next);
            }
        }

        return area;
    }

    private static bool IsFriendlyConductor(BoardManager board, byte ownerId, Vector2Int cell)
        => board.TryGetTile(cell.x, cell.y, out var t) && t.OwnerId == ownerId && t.ConductsConnectivity;
}
using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using UnityEngine;

public static class BoardUtilities
{
    private static BoardManager Manager { get; set; }

    public static void InstantiateBoardData(BoardManager boardManager, NetworkArray<TileState> tiles)
    {
        Manager = boardManager;
        Debug.Log("Instantiated board utilities.");
    }
    
    public static bool PawnElegabiltyCheckDFS([NotNull] TileState tileState, Vector2Int tileIndex, int playerId)
    {
        if (!Manager)
        {
            Debug.LogError("BoardManager instance is not set. Ensure InstantiateBoardData is called before using this method.");
            return false;
        }
        
        var checkedTileIndexes = new List<int>();
        var currentOwnerId = playerId;

        //check if the requested tile can become pawn
        if(!Manager.IsValidIndex(tileIndex) || !TileTransitions.CanRequest(tileState.Type, TileType.Soldier))
        {
            return false;
        }
        
        checkedTileIndexes.Add(Manager.ToIndex(tileIndex));
        
        return RecursiveHelperUDLRCheck(tileIndex, tileState, checkedTileIndexes, currentOwnerId);
    }


    private static bool RecursiveHelperUDLRCheck(Vector2Int currentTile, TileState targetTileState, List<int> checkedTileIndexes, int currentOwnerId)
    { // UDLR = up (0,1), down(0,-1), left(-1,0), right (1,0)

        // check if I am in bounds and reached a friendly base
        if (Manager.IsValidIndex(currentTile) &&
            Manager.TryGetTile(currentTile, out var tile) &&
            tile.Type == TileType.Base &&
            tile.OwnerId == currentOwnerId)
        {
            return true;
        }

        var directions = new[]
        {
            new Vector2Int(0, 1),   // up
            new Vector2Int(0, -1),  // down
            new Vector2Int(-1, 0),  // left
            new Vector2Int(1, 0),   // right
        };

        foreach (var direction in directions)
        {
            var nextTile = currentTile + direction;

            if (!Manager.IsValidIndex(nextTile))
                continue;

            var nextIndex = Manager.ToIndex(nextTile);
            if (checkedTileIndexes.Contains(nextIndex))
                continue;

            checkedTileIndexes.Add(nextIndex);

            if (!Manager.TryGetTile(nextTile, out var nextState))
                continue;

            // We can walk through friendly pawns, bombs, and stop at a friendly base.
            var isFriendlyPawn = nextState.Type == TileType.Soldier && nextState.OwnerId == currentOwnerId;
            var isFriendlyBase = nextState.Type == TileType.Base && nextState.OwnerId == currentOwnerId;
            var isFriendlyBomb = nextState.Type == TileType.Bomb && nextState.OwnerId == currentOwnerId;

            if (!isFriendlyPawn && !isFriendlyBase && !isFriendlyBomb)
                continue;

            if (RecursiveHelperUDLRCheck(nextTile, targetTileState, checkedTileIndexes, currentOwnerId))
                return true;
        }

        return false;
    }
    
    public static Queue<Vector2Int> DetonateBomb(Vector2Int epicenter)
    {
        var visitedBombs = new HashSet<Vector2Int>();
        var visitedAffected = new HashSet<Vector2Int>();
        var frontier = new Queue<Vector2Int>();
        var affected = new Queue<Vector2Int>();

        frontier.Enqueue(epicenter);
        visitedBombs.Add(epicenter);
        visitedAffected.Add(epicenter);
        affected.Enqueue(epicenter);

        Vector2Int size = Manager.GetSize();
        
        while (frontier.Count > 0)
        {
            var bombCell = frontier.Dequeue();

            foreach (var off in Diagonal8)
            {
                var n = bombCell + off;
                if (n.x < 0 || n.y < 0 || n.x >= size.x || n.y >= size.y)
                    continue;

                if (!Manager.TryGetTile(n, out var s))
                    continue;

                if (!s.IsBlastable)
                    continue;

                if (visitedAffected.Add(n))
                    affected.Enqueue(n);
                
                if (s.Type == TileType.Bomb && visitedBombs.Add(n))
                    frontier.Enqueue(n);
            }
        }
        
        return affected;
    }
    
    public static bool TryGetBottomLeftCornerOfBase4By4(Vector2Int startTile, byte owner, out  Vector2Int targetTile)
    {
        targetTile = default;

        if (!Manager)
        {
            Debug.LogError("BoardManager instance is not set. Ensure InstantiateBoardData is called before using this method.");
            return false;
        }
        
        // The start tile can be anywhere inside the 4x4.
        // Try every possible 4x4 bottom-left candidate that could contain it.
        for (var offsetX = 0; offsetX < 4; offsetX++)
        {
            for (var offsetY = 0; offsetY < 4; offsetY++)
            {
                var areaBottomLeft = new Vector2Int(startTile.x - offsetX, startTile.y - offsetY);
                var isValidArea = true;

                for (var x = areaBottomLeft.x; x < areaBottomLeft.x + 4 && isValidArea; x++)
                {
                    for (var y = areaBottomLeft.y; y < areaBottomLeft.y + 4; y++)
                    {
                        var currentTile = new Vector2Int(x, y);
                        if (!Manager.IsValidIndex(currentTile))
                        {
                            isValidArea = false;
                            break;
                        }

                        if (!Manager.TryGetTile(currentTile, out var tileState))
                        {
                            isValidArea = false;
                            break;
                        }

                        var isFriendlyPawn = tileState.Type == TileType.Soldier && tileState.OwnerId == owner;
                        var isFriendlyBomb = tileState.Type == TileType.Bomb && tileState.OwnerId == owner;

                        if (!isFriendlyPawn && !isFriendlyBomb)
                        {
                            isValidArea = false;
                            break;
                        }
                    }
                }

                if (!isValidArea)
                    continue;

                // Return the bottom-left tile of the inner 2x2 in this valid 4x4.
                targetTile = new Vector2Int(areaBottomLeft.x + 1, areaBottomLeft.y + 1);
                return true;
            }
        }

        return false;
    }
    
    
    private static readonly Vector2Int[] Diagonal8 =
    {
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    };
}
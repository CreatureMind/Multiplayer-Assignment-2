using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using UnityEngine;

public static class BoardUtilities
{
    private static BoardManager Manager { get; set; }
    private static NetworkArray<TileState> Tiles { get; set; }

    public static void InstantiateBoardData(BoardManager boardManager, NetworkArray<TileState> tiles)
    {
        Manager = boardManager;
        Tiles = tiles;
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
            Tiles[Manager.ToIndex(currentTile)].Type == TileType.Base &&
            Tiles[Manager.ToIndex(currentTile)].OwnerId == currentOwnerId)
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

            var nextState = Tiles[nextIndex];

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
    
    public static bool TryGetBottomLeftCornerOfBase4By4(Vector2Int startTile, PlayerRef owner, out  Vector2Int targetTile)
    {
        targetTile = default;

        if (!Manager)
        {
            Debug.LogError("BoardManager instance is not set. Ensure InstantiateBoardData is called before using this method.");
            return false;
        }

        var ownerId = owner.PlayerId;

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

                        var tileState = Tiles[Manager.ToIndex(currentTile)];
                        var isFriendlyPawn = tileState.Type == TileType.Soldier && tileState.OwnerId == ownerId;
                        var isFriendlyBomb = tileState.Type == TileType.Bomb && tileState.OwnerId == ownerId;

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
    
}
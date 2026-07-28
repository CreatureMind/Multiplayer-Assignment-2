using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class BoardManager : NetworkBehaviour
{
    public static BoardManager Instance { get; private set; }

    public const int MaxBoardTiles = 2500; // 50^2

    private int boardWidth = 8;
    private int boardHeight = 8;

    [Networked] public int Width { get; private set; }
    [Networked] public int Height { get; private set; }
    [Networked, Capacity(MaxBoardTiles)] private NetworkArray<TileState> Tiles => default;

    private Dictionary<Vector2Int, List<Vector2Int>> _baseCache = new();
    private  HashSet<Vector2Int> _motherloadCache = new();
    
    public int TileCount => Width * Height;

    #region Helper Methods

    public bool TryGetTile(int x, int y, out TileState tile)
    {
        if (!TryGetIndex(x, y, out var index))
        {
            tile = default;
            return false;
        }

        tile = Tiles[index];
        return true;
    }

    public int ToIndex(Vector2Int gridPosition)
    {
        return gridPosition.y * Width + gridPosition.x;
    }

    public int ToIndex(int x, int y)
    {
        return y * Width + x;
    }

    private bool TryGetIndex(int x, int y, out int index)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            index = -1;
            return false;
        }

        index = y * Width + x;
        return true;
    }

    private bool IsValidIndex(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool IsValidIndex(Vector2Int gridPosition)
    {
        return IsValidIndex(gridPosition.x, gridPosition.y) && Tiles[ToIndex(gridPosition)].Type != TileType.Blocked && Tiles[ToIndex(gridPosition)].Type != TileType.None;
    }

    #endregion

    #region Life Time Methods

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Runner.Despawn(Object);
            return;
        }

        Instance = this;
    }


    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void InitializeBoardWithMadeMap_ServerOnly(StartingPositionSO startingPosition)
    {
        // size 
        var size = ValidateBoardDimensions(startingPosition.Width, startingPosition.Height);
        Width = size.x;
        Height = size.y;
        
        var tempBaseCache = new HashSet<Vector2Int>();
        
        // copy the map 
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var index = ToIndex(x, y);
                var tileState = startingPosition.GetTileState(x, y);
                Tiles.Set(index, tileState);

                switch (tileState.Type)
                {
                    case TileType.Base when !tempBaseCache.Contains(new Vector2Int(x, y)):
                        tempBaseCache.Add(new Vector2Int(x, y));
                        continue;
                    case TileType.Motherload when !_motherloadCache.Contains(new Vector2Int(x, y)):
                        _motherloadCache.Add(new Vector2Int(x, y));
                        continue;
                }
            }
        }
        
        // cache the bases 
        CompileAndCacheAllBases(tempBaseCache);
        
        BoardUtilities.InstantiateBoardData(this, Tiles);
    }
    

    private void CompileAndCacheAllBases(HashSet<Vector2Int> baseTiles)
    {
        _baseCache.Clear();

        if (baseTiles == null || baseTiles.Count == 0)
            return;
        
        foreach (var bottomLeft in baseTiles)
        {
            var bottomRight = new Vector2Int(bottomLeft.x + 1, bottomLeft.y);
            var topLeft = new Vector2Int(bottomLeft.x, bottomLeft.y + 1);
            var topRight = new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1);

            if (!baseTiles.Contains(bottomRight) || !baseTiles.Contains(topLeft) || !baseTiles.Contains(topRight))
                continue;

            if (_baseCache.TryGetValue(bottomLeft, out _))
                continue;
            
            var baseTilesList = new List<Vector2Int>
            {
                bottomLeft,
                bottomRight,
                topLeft,
                topRight
            };
            
            _baseCache.Add(bottomLeft, baseTilesList);
        }
    }
    
    public HashSet<Vector2Int> CheckForConqueredBasesAndUpdateBoardState()
    {
        var updatedBottomLeftKeys = new HashSet<Vector2Int>();
        var keysToRefreshInCache = new List<Vector2Int>();

        if (_baseCache.Count == 0)
            return updatedBottomLeftKeys;

        foreach (var cacheEntry in _baseCache)
        {
            var bottomLeft = cacheEntry.Key;
            byte surroundingOwnerId = TileState.NoOwner;
            var allSurroundingTilesMatch = true;

            for (var y = bottomLeft.y - 1; y <= bottomLeft.y + 2 && allSurroundingTilesMatch; y++)
            {
                for (var x = bottomLeft.x - 1; x <= bottomLeft.x + 2; x++)
                {
                    var isInsideBaseCore = x >= bottomLeft.x && x <= bottomLeft.x + 1 &&
                                           y >= bottomLeft.y && y <= bottomLeft.y + 1;
                    if (isInsideBaseCore)
                        continue;

                    if (!TryGetIndex(x, y, out var surroundingIndex))
                    {
                        allSurroundingTilesMatch = false;
                        break;
                    }

                    var surroundingTile = Tiles[surroundingIndex];
                    if (surroundingTile.OwnerId == TileState.NoOwner)
                    {
                        allSurroundingTilesMatch = false;
                        break;
                    }

                    if (surroundingOwnerId == TileState.NoOwner)
                    {
                        surroundingOwnerId = surroundingTile.OwnerId;
                        continue;
                    }

                    if (surroundingOwnerId != surroundingTile.OwnerId)
                    {
                        allSurroundingTilesMatch = false;
                        break;
                    }
                }
            }

            if (!allSurroundingTilesMatch)
                continue;

            var baseTiles = cacheEntry.Value is { Count: 4 }
                ? cacheEntry.Value
                : new List<Vector2Int>
                {
                    bottomLeft,
                    new Vector2Int(bottomLeft.x + 1, bottomLeft.y),
                    new Vector2Int(bottomLeft.x, bottomLeft.y + 1),
                    new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1)
                };

            var wasUpdated = false;
            foreach (var baseTilePosition in baseTiles)
            {
                if (!TryGetIndex(baseTilePosition.x, baseTilePosition.y, out var baseTileIndex))
                    continue;

                var currentState = Tiles[baseTileIndex];
                if (currentState.Type != TileType.Base || currentState.OwnerId == surroundingOwnerId)
                    continue;

                Tiles.Set(baseTileIndex, currentState.WithOwner(surroundingOwnerId));
                wasUpdated = true;
            }

            if (!wasUpdated)
                continue;

            updatedBottomLeftKeys.Add(bottomLeft);
            keysToRefreshInCache.Add(bottomLeft);
        }

        foreach (var bottomLeft in keysToRefreshInCache)
        {
            _baseCache[bottomLeft] = new List<Vector2Int>
            {
                bottomLeft,
                new Vector2Int(bottomLeft.x + 1, bottomLeft.y),
                new Vector2Int(bottomLeft.x, bottomLeft.y + 1),
                new Vector2Int(bottomLeft.x + 1, bottomLeft.y + 1)
            };
        }

        return updatedBottomLeftKeys;
    }

    #endregion

    #region Board Change Validation
    private static Vector2Int ValidateBoardDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            Debug.Log("Board dimensions must be greater than zero.");
            return Vector2Int.one;
        }

        if (width * height > MaxBoardTiles)
        {
            Debug.Log($"Board dimensions exceed max tile capacity ({MaxBoardTiles}).");
            return Vector2Int.one;
        }

        Debug.Log($"Board dimensions are: {width}x{height}.");
        return new Vector2Int(width, height);
    }

    public bool ValidateBoardChange(Vector2Int gridPosition, int playerId , MoveIntent intent)
    {
        switch (intent)
        {
            case MoveIntent.MoveSoldier:
                return PawnCheck(gridPosition, playerId);
            case MoveIntent.PlaceBomb:
                return BombCheck(gridPosition, playerId);
            case MoveIntent.BuildBase:
                return BaseCheck(gridPosition, playerId);
            default:
                return false;
        }
    }

    private bool BaseCheck(Vector2Int gridPosition, int playerId) // TODO
    {
        return BoardUtilities.PawnElegabiltyCheckDFS(Tiles[ToIndex(gridPosition)], gridPosition, playerId);
    }

    private bool PawnCheck(Vector2Int gridPosition, int playerId)
    {
        return BoardUtilities.PawnElegabiltyCheckDFS(Tiles[ToIndex(gridPosition)], gridPosition, playerId);
    }

    private bool BombCheck(Vector2Int gridPosition, int playerId)
    {
        return BoardUtilities.PawnElegabiltyCheckDFS(Tiles[ToIndex(gridPosition)], gridPosition, playerId);
    }
    #endregion

    // never call without the correct checks
    #region Server Only Board Change Methods

    public void SetTileServerOnly(Vector2Int gridPosition, int playerId, MoveIntent intent)
    {
        switch (intent)
        {
            case MoveIntent.MoveSoldier:
                SetPawn(gridPosition, playerId);
                break;
            case MoveIntent.PlaceBomb:
                SetBomb(gridPosition, playerId);
                break;
            case MoveIntent.BuildBase:
                SetBase(gridPosition, playerId);
                break;
            default:
                return;
        }
    }

    private void SetBase(Vector2Int gridPosition, int playerId)
    {
        for (var y = gridPosition.y + 1; y <= gridPosition.y + 2; y++)
        {
            for (var x = gridPosition.x + 1; x <= gridPosition.x + 2; x++)
            {
                if (!TryGetIndex(x, y, out var index))
                    continue;

                var tile = Tiles[index];
                // Writes authoritative Base core cells so board diffs carry the full 2x2 BuildBase mutation.
                var updatedTile = TileState.BaseCell((byte)playerId, tile.TerritoryId);
                Tiles.Set(index, updatedTile);
            }
        }
        
        // TODO
        
    }

    private void SetBomb(Vector2Int gridPosition, int playerId)
    {
        var index = ToIndex(gridPosition);
        // Writes authoritative Bomb state so board diffs carry the actual mutation.
        var updatedTile = TileState.Bomb((byte)playerId);
        Tiles.Set(index, updatedTile);
        
        // broadcast diff
    }

    private void SetPawn(Vector2Int gridPosition, int playerId)
    {
        var index = ToIndex(gridPosition);
        // Writes authoritative Soldier state so board diffs carry the actual mutation.
        var updatedTile = TileState.Soldier((byte)playerId);
        Tiles.Set(index, updatedTile);
        
        // broadcast diff 
    }
    
    private void ExistingBaseConqueredByPlayer(Vector2Int bottomLeft, int playerId)
    {
        if (!_baseCache.TryGetValue(bottomLeft, out var baseTiles))
            return;

        foreach (var baseTilePosition in baseTiles)
        {
            if (!TryGetIndex(baseTilePosition.x, baseTilePosition.y, out var baseTileIndex))
                continue;

            var currentState = Tiles[baseTileIndex];
            if (currentState.Type != TileType.Base || currentState.OwnerId == playerId)
                continue;

            Tiles.Set(baseTileIndex, currentState.WithOwner((byte)playerId));
        }
    }

    #endregion


    public int GetTileOwnerByIndex(Vector2Int tileIndex)
    {
        var index = ToIndex(tileIndex);
        var tile = Tiles[index];
        return tile.OwnerId;
    }
}
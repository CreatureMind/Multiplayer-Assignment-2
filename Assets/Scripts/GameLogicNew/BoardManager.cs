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
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
    [Networked, Capacity(MaxBoardTiles)] private NetworkArray<TileState> Tiles => default;

    private Dictionary<Vector2Int, List<Vector2Int>> _baseCache = new();
    private  HashSet<Vector2Int> _motherloadCache = new();
    
    public int TileCount => Width * Height;

    #region Grid Access and Indexing

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

    public bool TryGetTile(Vector2Int position, out TileState tile)
    {
        if (!TryGetIndex(position.x, position.y, out var index))
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

    #region Lifecycle and Initialization

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

    public void SetTraceLoggingEnabled(NetworkBool enabled)
    {
        if (!HasStateAuthority)
            return;

        TraceLogsEnabled = enabled;
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
    

    #endregion

    #region Base Compilation and Conquest

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
    
    public HashSet<Vector2Int> CheckForConqueredBasesAndUpdateBoardState(bool includeAlreadyConquered = true)
    {
        GameTraceLogger.Board(TraceLogsEnabled, $"Checking conquered bases. Cached bases={_baseCache.Count}.");
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
            {
                GameTraceLogger.Board(TraceLogsEnabled, $"Base at {bottomLeft} is not conquered: surrounding ring is not uniformly owned.");
                continue;
            }

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

            if (wasUpdated)
            {
                GameTraceLogger.Board(TraceLogsEnabled, $"Conquered base ownership updated for base at {bottomLeft} to owner {surroundingOwnerId}.");
            }
            else
            {
                GameTraceLogger.Board(TraceLogsEnabled, $"Base at {bottomLeft} is conquered by owner {surroundingOwnerId}, but no ownership change was needed.");
            }

            if (wasUpdated || includeAlreadyConquered)
            {
                updatedBottomLeftKeys.Add(bottomLeft);
                keysToRefreshInCache.Add(bottomLeft);
            }
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

    #region Move Validation
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

    public ValidationType ValidateBoardChange(Vector2Int gridPosition, int playerId , MoveIntent intent)
    {
        GameTraceLogger.Board(TraceLogsEnabled, $"ValidateBoardChange player={playerId}, intent={intent}, cell={gridPosition}.");
        switch (intent)
        {
            case MoveIntent.PlaceBomb:
            case MoveIntent.MoveSoldier:
            {
                var result = PawnCheck(gridPosition, playerId);
                GameTraceLogger.Board(TraceLogsEnabled, $"PawnCheck result for player={playerId}, cell={gridPosition}: {result}.");
                return result;
            }
            case MoveIntent.BuildBase:
            {
                var result = BaseCheck(gridPosition, playerId);
                GameTraceLogger.Board(TraceLogsEnabled, $"BaseCheck result for player={playerId}, cell={gridPosition}: {result}.");
                return result ? ValidationType.True : ValidationType.False;
            }
            default:
                GameTraceLogger.Board(TraceLogsEnabled, $"ValidateBoardChange rejected unknown intent {intent}.");
                return ValidationType.False;
        }
    }

    private bool BaseCheck(Vector2Int gridPosition, int playerId)
    {
        return ServerBoardRules.IsBaseWindow(this, (byte)playerId, gridPosition);
    }

    private ValidationType PawnCheck(Vector2Int gridPosition, int playerId)
    {
        var con = BoardUtilities.PawnElegabiltyCheckDFS(Tiles[ToIndex(gridPosition)], gridPosition, playerId);
        if (!con)
            return ValidationType.False;
        
        if (TryGetTile(gridPosition.x, gridPosition.y, out var tile) && tile.Type == TileType.Bomb && tile.OwnerId != playerId)
            return ValidationType.Bomb;
        
        return ValidationType.True;
        
    }
    
    #endregion

    // never call without the correct checks
    #region Server Board Mutations

    public void SetTileServerOnly(Vector2Int gridPosition, int playerId, MoveIntent intent)
    {
        GameTraceLogger.Board(TraceLogsEnabled, $"SetTileServerOnly player={playerId}, intent={intent}, cell={gridPosition}.");
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

    public bool SetTileEmptyServerOnly(Vector2Int gridPosition)
    {
        if (!TryGetIndex(gridPosition.x, gridPosition.y, out var index))
            return false;

        var currentTile = Tiles[index];
        if (!currentTile.IsBlastable)
            return false;

        Tiles.Set(index, TileState.Empty);
        GameTraceLogger.Board(TraceLogsEnabled, $"SetTileEmpty applied at cell={gridPosition} from type={currentTile.Type}.");
        return true;
    }

    private void SetBase(Vector2Int gridPosition, int playerId)
    {
        GameTraceLogger.Board(TraceLogsEnabled, $"SetBase start player={playerId}, buildOrigin={gridPosition}.");
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
        GameTraceLogger.Board(TraceLogsEnabled, $"SetBase completed player={playerId}, buildOrigin={gridPosition}.");
        
    }

    private void SetBomb(Vector2Int gridPosition, int playerId)
    {
        var index = ToIndex(gridPosition);
        // Writes authoritative Bomb state so board diffs carry the actual mutation.
        var updatedTile = TileState.Bomb((byte)playerId);
        Tiles.Set(index, updatedTile);
        GameTraceLogger.Board(TraceLogsEnabled, $"SetBomb applied player={playerId}, cell={gridPosition}.");
        
        // broadcast diff
    }

    private void SetPawn(Vector2Int gridPosition, int playerId)
    {
        var index = ToIndex(gridPosition);
        // Writes authoritative Soldier state so board diffs carry the actual mutation.
        var updatedTile = TileState.Soldier((byte)playerId);
        Tiles.Set(index, updatedTile);
        GameTraceLogger.Board(TraceLogsEnabled, $"SetPawn applied player={playerId}, cell={gridPosition}.");
        
        // broadcast diff 
    }
    
    #endregion

    #region Public Queries


    public int GetTileOwnerByIndex(Vector2Int tileIndex)
    {
        var index = ToIndex(tileIndex);
        var tile = Tiles[index];
        return tile.OwnerId;
    }

    public Vector2Int GetSize()
    {
        return new Vector2Int(Width, Height);
    }

    #endregion
}


public enum ValidationType
{
    True,
    False,
    Bomb
}
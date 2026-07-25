using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class BoardManager : NetworkBehaviour
{
    public static BoardManager Instance { get; private set; }

    public const int MaxBoardTiles = 1024;

    [SerializeField, Min(1)] private int boardWidth = 8;
    [SerializeField, Min(1)] private int boardHeight = 8;

    [Networked] public int Width { get; private set; }
    [Networked] public int Height { get; private set; }
    [Networked, Capacity(MaxBoardTiles)] private NetworkArray<TileState> Tiles => default;

    private ChangeDetector _changeDetector;
    private readonly BoardChangeCheck _changeCheck = new();

    public int TileCount => Width * Height;
    public int ChangeVersion => _changeCheck.Version;

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

    private bool TrySetTileServerOnly(int x, int y, in TileState state)
    {
        if (!HasStateAuthority)
            return false;

        if (!TryGetIndex(x, y, out var index))
            return false;

        Tiles.Set(index, state);
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


        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        BoardUtilities.InstantiateBoardData(this, Tiles);

        if (HasStateAuthority)
        {
            ValidateBoardDimensions(boardWidth, boardHeight);
            Width = boardWidth;
            Height = boardHeight;
            InitializeBoard(new TileState(TileType.Empty));
        }

        NotifyVisualsChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _changeCheck.Clear();

        if (Instance == this)
            Instance = null;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(Tiles) || change == nameof(Width) || change == nameof(Height))
            {
                NotifyVisualsChanged();
                break;
            }
        }
    }

    #endregion

    public void RegisterVisualRenderer(Action<int> onBoardChanged, bool replayLatest = true)
        => _changeCheck.Subscribe(onBoardChanged, replayLatest);

    public void UnregisterVisualRenderer(Action<int> onBoardChanged)
        => _changeCheck.Unsubscribe(onBoardChanged);


    private void InitializeBoard(in TileState initialState)
    {
        var count = TileCount;
        for (var index = 0; index < count; index++)
            Tiles.Set(index, initialState);
    }


    private void NotifyVisualsChanged()
        => _changeCheck.NotifyVisualsChanged();

    private static void ValidateBoardDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Board dimensions must be greater than zero.");

        if (width * height > MaxBoardTiles)
            throw new InvalidOperationException($"Board dimensions exceed max tile capacity ({MaxBoardTiles}).");
    }

    public bool ValidateBoardChange(Vector2Int gridPosition, TileType targetType)
    {
        switch (targetType)
        {
            case TileType.None:
                return false;

            case TileType.Empty:
                return false;

            case TileType.Bomb:
                return BombCheck(gridPosition);

            case TileType.Soldier:
                return PawnCheck(gridPosition);

            case TileType.Base:
                return BaseCheck(gridPosition);

            case TileType.Motherload:
                return false;

            default:
                return false;
        }
    }

    private bool BaseCheck(Vector2Int gridPosition)
    {
        return false;
    }

    private bool PawnCheck(Vector2Int gridPosition)
    {
        return false;
    }

    private bool BombCheck(Vector2Int gridPosition)
    {
        return false;
    }
}


internal sealed class BoardChangeCheck
{
    private readonly List<Action<int>> _renderers = new();
    private int _version;

    public int Version => _version;

    public void Subscribe(Action<int> renderer, bool replayLatest)
    {
        if (renderer == null)
            return;

        _renderers.Add(renderer);

        if (replayLatest && _version > 0)
            renderer.Invoke(_version);
    }

    public void Unsubscribe(Action<int> renderer)
    {
        if (renderer == null)
            return;

        _renderers.Remove(renderer);
    }

    public void NotifyVisualsChanged()
    {
        _version++;
        foreach (var t in _renderers)
            t.Invoke(_version);
    }

    public void Clear()
    {
        _renderers.Clear();
        _version = 0;
    }
}
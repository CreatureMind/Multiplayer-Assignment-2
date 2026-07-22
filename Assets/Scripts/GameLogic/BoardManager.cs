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
    
    public ServerBoardMutator ServerMutator { get; private set; }

    private ChangeDetector _changeDetector;
    private readonly BoardChangeCheck _changeCheck = new();

    public int TileCount => Width * Height;
    public int ChangeVersion => _changeCheck.Version;

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Runner.Despawn(Object);
            return;
        }

        Instance = this;
        
        
        ServerMutator = new ServerBoardMutator(this);
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

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
        ServerMutator = null;

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

    public void RegisterVisualRenderer(Action<int> onBoardChanged, bool replayLatest = true)
        => _changeCheck.Subscribe(onBoardChanged, replayLatest);

    public void UnregisterVisualRenderer(Action<int> onBoardChanged)
        => _changeCheck.Unsubscribe(onBoardChanged);

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

    private void InitializeBoard(in TileState initialState)
    {
        var count = TileCount;
        for (var index = 0; index < count; index++)
            Tiles.Set(index, initialState);
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

    private void NotifyVisualsChanged()
        => _changeCheck.NotifyVisualsChanged();

    private static void ValidateBoardDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Board dimensions must be greater than zero.");

        if (width * height > MaxBoardTiles)
            throw new InvalidOperationException($"Board dimensions exceed max tile capacity ({MaxBoardTiles}).");
    }

    public sealed class ServerBoardMutator
    {
        private readonly BoardManager _board;

        internal ServerBoardMutator(BoardManager board)
        {
            _board = board;
        }

        public bool TrySetTile(int x, int y, in TileState state)
            => _board.TrySetTileServerOnly(x, y, state);

        public bool TryClearTile(int x, int y)
            => _board.TrySetTileServerOnly(x, y, new TileState(TileType.Empty));
    }

    

    public bool ValidateBoardChange(Vector2Int gridPosition, TileType targetType)
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
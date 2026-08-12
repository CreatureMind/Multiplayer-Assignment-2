using System.Collections.Generic;
using UnityEngine;

public sealed class ClientConnectivityMap
{
    private static readonly Vector2Int[] Orthogonal4 =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };
    
    private readonly ClientBoardCache _board;
    private readonly byte _localPlayerId;

    // Reachable cells from a friendly root. NOT frozen. 
    private readonly HashSet<Vector2Int> _live = new();
    
    // Reused flood frontier so steady-state recomputes don't allocate.
    private readonly Stack<Vector2Int> _frontier = new();
    
    private bool _dirty = true;
    
    public ClientConnectivityMap(ClientBoardCache board, byte localPlayerId)
    {
        _board = board;
        _localPlayerId = localPlayerId;
        _board.Changed += OnBoardChanged;
    }
    
    public void Dispose() => _board.Changed -= OnBoardChanged;
    
    private void OnBoardChanged(IReadOnlyList<CellDiff> _) => _dirty = true;
    
    public bool IsLive(Vector2Int cell)
    {
        EnsureCurrent();
        return _live.Contains(cell);
    }
    
    public bool IsFrozen(Vector2Int cell)
    {
        EnsureCurrent();
        return IsMyConductor(cell) && !_live.Contains(cell);
    }
    
    public void EnsureCurrent()
    {
        if (!_dirty)
            return;
        Recompute();
        _dirty = false;
    }
    
    private void Recompute()
    {
        _live.Clear();
        _frontier.Clear();

        // Seed from every friendly root (owned Base or owned Motherload).
        for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsMyRoot(_board[cell]) && _live.Add(cell))
                    _frontier.Push(cell);
            }

        // Flood 4-connected across my conductors.
        while (_frontier.Count > 0)
        {
            var cell = _frontier.Pop();
            foreach (var offset in Orthogonal4)
            {
                var n = cell + offset;
                if (IsMyConductor(n) && _live.Add(n))
                    _frontier.Push(n);
            }
        }
    }
    
    private bool IsMyRoot(in TileView view)
        => view.OwnerId == _localPlayerId
           && view.VisualType is TileType.Base or TileType.Motherload;
    
    private bool IsMyConductor(Vector2Int cell)
    {
        if (!_board.Contains(cell))
            return false;
        var view = _board[cell];
        return view.OwnerId == _localPlayerId && view.ConductsConnectivity;
    }
}
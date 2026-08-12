using System.Collections.Generic;
using UnityEngine;

// Works out which cells the local player may act on right now.
// Only the OUTCOME of a capture is unknowable. The server still revalidates every request.
public sealed class LegalMoveCalculator
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
    private readonly ClientConnectivityMap _connectivity;
    
    // Reused across recomputes, so this allocates only on first warm-up
    private readonly HashSet<Vector2Int> _moveTargets = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _bombTargets = new HashSet<Vector2Int>();
    
    // Empty cells I can expand into, plus enemy soldiers I can capture
    public IReadOnlyCollection<Vector2Int> MoveTargets => _moveTargets;
    
    // My own live soldiers, each of which a bomb could replace
    public IReadOnlyCollection<Vector2Int> BombTargets => _bombTargets;

    public LegalMoveCalculator(ClientBoardCache board, byte localPlayerId, ClientConnectivityMap connectivity)
    {
        _board = board;
        _localPlayerId = localPlayerId;
        _connectivity = connectivity;
    }
    
    // O(1) membership tests.
    // Exposed as methods so callers get HashSet lookup directly instead of going through Enumerable.Contains and its runtime ICollection check
    public bool IsMoveTarget(Vector2Int cell) => _moveTargets.Contains(cell);
    public bool IsBombTarget(Vector2Int cell) => _bombTargets.Contains(cell);
    
    public void Recompute()
    {
        _moveTargets.Clear();
        _bombTargets.Clear();
        
        for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var view = _board[cell];
                
                if (view.OwnerId != _localPlayerId)
                    continue;
                
                var isLive = _connectivity.IsLive(cell);
                
                if (isLive && view.VisualType == TileType.Soldier)
                    _bombTargets.Add(cell);
                
                if (!isLive)
                    continue;

                foreach (var offset in Orthogonal4)
                {
                    var neighbour = cell + offset;
                    if (!_board.Contains(neighbour))
                        continue;
                    if (CanMoveInto(_board[neighbour]))
                        _moveTargets.Add(neighbour);
                }
            }
    }

    // An empty cell, or an enemy Soldier.
    // Enemy Bombs project as Soldiers and are therefor included on purpose.
    private bool CanMoveInto(in TileView view)
    {
        if (view.VisualType == TileType.Empty)
            return true;

        return view.VisualType == TileType.Soldier && view.OwnerId != _localPlayerId;
    }
}
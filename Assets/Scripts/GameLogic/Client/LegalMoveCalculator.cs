using System.Collections.Generic;
using UnityEngine;

// Works out which cells the local player may act on right now.
// This is NOT prediction and needs no round trip: the player sees all of their own cells truthfully,
// and legality never depends on an enemy cell's hidden type.
// Only the OUTCOME of a capture is unknowable. The server still revalidates every request.
// The server already sends Frozen per cell, so no BFS is needed here.
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
    
    // Reused across recomputes, so this allocates only on first warm-up
    private readonly HashSet<Vector2Int> _moveTargets = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _bombTargets = new HashSet<Vector2Int>();
    
    // Empty cells I can expand into, plus enemy soldiers I can capture
    public IReadOnlyCollection<Vector2Int> MoveTargets => _moveTargets;
    
    // My own live soldiers, each of which a bomb could replace
    public IReadOnlyCollection<Vector2Int> BombTargets => _bombTargets;

    public LegalMoveCalculator(ClientBoardCache board, byte localPlayerId)
    {
        _board = board;
        _localPlayerId = localPlayerId;
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
                
                // A bomb replaces a soldier, so bombs and bases are not valid hosts
                if (view.VisualType == TileType.Soldier)
                    _bombTargets.Add(cell);
                
                if (view.Frozen)
                    continue;
                
                if (!view.ConductsConnectivity)
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
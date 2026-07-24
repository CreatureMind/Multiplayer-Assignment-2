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
                if (!CanExtendFrom(view.VisualType))
                    continue;

                foreach (var t in Orthogonal4)
                {
                    var n = cell + t;
                    if (!_board.Contains(n))
                        continue;
                    if (IsMoveTarget(_board[n]))
                        _moveTargets.Add(n);
                }
            }
    }

    // Cells that conduct my connectivity outward
    private static bool CanExtendFrom(TileType type)
        => type is TileType.Soldier or TileType.Bomb or TileType.Base or TileType.Motherload;
    
    // An empty cell, or an enemy soldier.
    // Enemy bombs project as soldiers, so they are included here on purpose - that is the whole mechanic
    private bool IsMoveTarget(in TileView view)
    {
        if (view.VisualType == TileType.Empty)
            return true;
        return view.VisualType == TileType.Soldier && view.OwnerId != _localPlayerId;
    }
}
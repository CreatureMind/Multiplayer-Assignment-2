using Fusion;
using UnityEngine;

public enum MoveIntent : byte
{
    MoveSoldier = 0, // into an empty cell, or onto an enemy soldier (capture)
    PlaceBomb = 1, // replaces one of my own soldiers; costs 3 budget
    BuildBase = 2, // converts the middle 2x2 of a 4x4 block; ends the turn
    Pass = 3 // end turn early
}

// Definition of client -> server request. cell means different things per intent:
// - MoveSoldier -> the target cell
// - PlaceBomb -> my own soldier being replaced
// - BuildBase -> the BOTTOM_LEFT corner of the chosen 4x4 window
// - Pass -> ignored
public readonly struct MoveRequest
{
    public readonly MoveIntent Intent;
    public readonly Vector2Int Cell;

    public MoveRequest(MoveIntent intent, Vector2Int cell)
    {
        Intent = intent;
        Cell = cell;
    }
    
    public static MoveRequest Pass => new MoveRequest(MoveIntent.Pass, Vector2Int.zero);
    public override string ToString() => $"{Intent}@{Cell}";
}

// One changed cell, already projected by the server for THIS viewer.
// Deliberately flat primitives: this goes over an RPC, so every field must be Fusion-serializable.
// Frozen is a byte flag rather than a bool for the same reason. 8 bytes per cell.
public struct CellDiff : INetworkStruct
{
    public short X;
    public short Y;
    public byte VisualType; // (byte)TileType, post-projection
    public byte OwnerId;
    public byte Frozen; // 0 or 1
    public byte Generation; // blast wave index; 0 = not part of a blast
    
    public Vector2Int Cell => new Vector2Int(X, Y);
    
    public readonly TileView ToView()
        => new TileView((TileType)VisualType, OwnerId, Frozen != 0);

    public static CellDiff From(Vector2Int cell, TileType visual, byte owner, bool frozen, byte generation = 0)
        => new CellDiff
        {
            X = (short)cell.x, Y = (short)cell.y,
            VisualType = (byte)visual, OwnerId = owner,
            Frozen = frozen ? (byte)1 : (byte)0, Generation = generation
        };
}
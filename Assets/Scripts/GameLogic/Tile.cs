using System;
using Fusion;

public enum TileType : byte
{
    None = 0,
    Empty = 1,
    Pawn = 2,
    Bomb = 3,
    Base = 4,
    Blocked = 5,
    Core = 6
}

[Serializable]
public readonly struct TileState : INetworkStruct, IEquatable<TileState>
{
    public const byte NoOwner = 0;
    public const short NoTerritory = 0;

    public readonly TileType Type;
    public readonly int OwnerId;
    public readonly short TerritoryId;

    public TileState(TileType type, int ownerId = NoOwner, short territoryId = NoTerritory)
    {
        Type = type;
        OwnerId = ownerId;
        TerritoryId = territoryId;
    }

    public static TileState Empty => new TileState(TileType.Empty);
    public static TileState Blocked => new TileState(TileType.Blocked);
    public static TileState Pawn(byte owner) => new TileState(TileType.Pawn, owner);
    public static TileState Bomb(byte owner) => new TileState(TileType.Bomb, owner);
    public static TileState BaseCell(byte owner, short territoryId) => new TileState(TileType.Base, owner, territoryId);
    public static TileState CoreCell(short territoryId) => new TileState(TileType.Core, NoOwner, territoryId);

    public bool IsEmpty => Type == TileType.Empty;

    public bool IsBlastable => Type is TileType.Pawn or TileType.Bomb;

    public bool IsFormationUnit => Type is TileType.Pawn or TileType.Bomb;

    public bool IsOwnedBy(byte playerId) => playerId != NoOwner && OwnerId == playerId;

    public TileState WithOwner(byte newOwner) => new TileState(Type, newOwner, TerritoryId);

    public TileState WithTerritory(short territoryId) => new TileState(Type, OwnerId, territoryId);

    public bool Equals(TileState other)
        => Type == other.Type && OwnerId == other.OwnerId && TerritoryId == other.TerritoryId;

    public override bool Equals(object obj) => obj is TileState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Type, OwnerId, TerritoryId);

    public static bool operator ==(TileState a, TileState b) => a.Equals(b);
    public static bool operator !=(TileState a, TileState b) => !a.Equals(b);

    public override string ToString()
        => TerritoryId == NoTerritory ? $"{Type}(p{OwnerId})" : $"{Type}(p{OwnerId},t{TerritoryId})";
}

public readonly struct TileView
{
    public readonly TileType VisualType; // Bomb reads as Pawn to non-owners
    public readonly int OwnerId;
    public readonly bool Frozen; // render hint only - derived, never stored

    public TileView(TileType visualType, int ownerId, bool frozen)
    {
        VisualType = visualType;
        OwnerId = ownerId;
        Frozen = frozen;
    }
}

public static class TileProjector
{
    public static TileView Project(in TileState state, int viewerId, bool frozen)
    {
        var hidden = state.Type == TileType.Bomb && state.OwnerId != viewerId;
        return new TileView(hidden ? TileType.Pawn : state.Type, state.OwnerId, frozen);
    }
}

public static class TileTransitions
{
    private static readonly TileType[] Allowed =
    {
        TileType.Pawn,
        TileType.Empty | TileType.Bomb | TileType.Base,
        TileType.Empty, // detonation only
        TileType.None, // type-terminal; owner still mutable
        TileType.None,
        TileType.None, // type-terminal; owner change ends the game
    };

    private static readonly TileType[] Requestable =
    {
        TileType.Pawn, // place
        TileType.Bomb | TileType.Base, // upgrade, build
        TileType.None,
        TileType.None,
        TileType.None,
        TileType.None,
    };

    static TileTransitions()
    {
        var count = Enum.GetValues(typeof(TileType)).Length;
        if (Allowed.Length != count || Requestable.Length != count)
            throw new InvalidOperationException(
                $"TileTransitions tables ({Allowed.Length}/{Requestable.Length}) out of sync with TileType ({count}).");

        for (var i = 0; i < count; i++)
            if ((Requestable[i] & ~Allowed[i]) != 0)
                throw new InvalidOperationException(
                    $"Requestable[{(TileType)i}] permits transitions Allowed does not.");
    }

    private static TileType MaskOf(TileType t) => (TileType)(1 << (int)t);

    public static bool CanBecome(TileType from, TileType to)
        => (Allowed[(int)from] & MaskOf(to)) != 0;

    public static bool CanRequest(TileType from, TileType to)
        => (Requestable[(int)from] & MaskOf(to)) != 0;
}
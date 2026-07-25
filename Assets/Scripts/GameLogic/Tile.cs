using System;
using Fusion;

// Authoritative tile type, matching the GDD vocabulary.
// NOTE: values are explicit and MUST NOT be reordered or have entries inserted mid-list.
// TileTransitions indexes its table by (int)TileType and derives bit positions from these values;
// the static constructor will throw at startup if the two ever drift apart.
// None is a deliberate sentinel meaning "no data".
// On the client it marks a cell no diff has arrived for yet, which is distinct from Empty (real state).
public enum TileType : byte
{
    None = 0,
    Empty = 1,
    Soldier = 2,
    Bomb = 3,
    Base = 4,
    Blocked = 5, // authored terrain for non-rectangular maps
    Motherload = 6 // the central win-condition territory
}

// Bit-per-type set used by the transition tables.
// Each member's bit position is derived from the matching TileType value, so the two cannot drift.
// ushort rather than byte purely for headroom past 8 tile types.
[Flags]
public enum TileTypeMask : ushort
{
    None = 0,
    Empty = 1 << TileType.Empty,
    Soldier = 1 << TileType.Soldier,
    Bomb = 1 << TileType.Bomb,
    Base = 1 << TileType.Base,
    Blocked = 1 << TileType.Blocked,
    Motherload = 1 << TileType.Motherload
}

// One authoritative cell.
// Pure C#: no UnityEngine, no Fusion.
// It compiles into a headless server build or a plain NUnit unchanged, which is what keeps BoardGraph testable without an editor.
// Readonly by design: mutation replaces the whole struct, so "GameManager is the only writer" is enforced by the compiler rather than by convention.
// Frozen is deliberately absent. It is derived every time the board mutates and lives only in TileView.
// There is nowhere to accidentally persist it.
[Serializable]
public readonly struct TileState : INetworkStruct, IEquatable<TileState>
{
    public const byte NoOwner = 0; // player ids are 1-based
    public const short NoTerritory = 0;

    public readonly TileType Type;
    public readonly byte OwnerId;
    public readonly short TerritoryId; // links Base/Motherload cells to a Territory record

    public TileState(TileType type, byte ownerId = NoOwner, short territoryId = NoTerritory)
    {
        Type = type;
        OwnerId = ownerId;
        TerritoryId = territoryId;
    }

    // Factories
    // Named constructors make invalid combinations unrepresentable at the call site.
    // You cannot accidentally author a Blocked cell with an owner.
    public static TileState Empty => new TileState(TileType.Empty);
    public static TileState Blocked => new TileState(TileType.Blocked);
    public static TileState Soldier(byte owner) => new TileState(TileType.Soldier, owner);
    public static TileState Bomb(byte owner) => new TileState(TileType.Bomb, owner);
    public static TileState BaseCell(byte owner, short territoryId) => new TileState(TileType.Base, owner, territoryId);
    public static TileState MotherloadCell(short territoryId) => new TileState(TileType.Motherload, NoOwner, territoryId);

    // Predicates
    // Each one names a rule from the GDD.
    // Call sites ask questions rather than switching on Type, so a rule change lands in exactly one place.
    public bool IsEmpty => Type == TileType.Empty;
    public bool IsUnknown => Type == TileType.None;

    // Cells a blast converts to Empty. Bases, Motherload and Blocked survive.
    // Bases are conquered by ring, never destroyed.
    public bool IsBlastable => Type is TileType.Soldier or TileType.Bomb;

    // Cells an enemy can walk onto to take ownership. Soldiers only.
    // An enemy bomb projects as a Soldier to the attacker, so the client requests a capture and the server resolves it into a detonation instead.
    public bool IsCapturable => Type is TileType.Soldier;
    
    // Valid in the outer 12 of a 4x4 base-build window.
    public bool IsFormationUnit => Type is TileType.Soldier or TileType.Bomb;

    // Valid in the middle 2x2 of a base-build window. Bombs are excluded.
    // Only plain Soldiers convert into base cells.
    public bool IsFormationCore => Type is  TileType.Soldier;
    
    // Cells that carry a player's chain outward.
    // Combined with an ownership check this is the flood fill's traversal predicate, and the same predicate the client uses to derive legal moves.
    public bool ConductsConnectivity
        => Type is TileType.Soldier or TileType.Bomb or TileType.Base or TileType.Motherload;
    
    // Part of a conquerable region. Territory records key off TerritoryId.
    public bool IsTerritory => Type is TileType.Base or TileType.Motherload;
    
    // Blocked and Motherload carry NoOwner until conquered, so they fall out of traversal automatically without a special case.
    public bool IsOwnedBy(byte playerId) => playerId != NoOwner && OwnerId == playerId;

    // Non-mutating transforms
    // Capture and conquest: type and territory link are preserved, only ownership moves.
    // This is why type and owner are separate fields, and why no self-transition (Soldier -> Soldier) appears in the tables below.
    public TileState WithOwner(byte newOwner) => new TileState(Type, newOwner, TerritoryId);
    public TileState WithTerritory(short territoryId) => new TileState(Type, OwnerId, territoryId);

    // Equality
    // IEquatable so differing (emit a CellDiff only when a cell really changed) never boxes.
    public bool Equals(TileState other)
        => Type == other.Type && OwnerId == other.OwnerId && TerritoryId == other.TerritoryId;
    public override bool Equals(object obj) => obj is TileState other && Equals(other);
    
    public override int GetHashCode() => HashCode.Combine(Type, OwnerId, TerritoryId);

    public static bool operator ==(TileState a, TileState b) => a.Equals(b);
    public static bool operator !=(TileState a, TileState b) => !a.Equals(b);

    public override string ToString()
        => TerritoryId == NoTerritory ? $"{Type}(p{OwnerId})" : $"{Type}(p{OwnerId},t{TerritoryId})";
}

// Table-driven TYPE transition rules.
public static class TileTransitions
{
    // What the rules permit at all
    private static readonly TileTypeMask[] Allowed =
    {
        /* None       */TileTypeMask.None,
        /* Empty      */TileTypeMask.Soldier,
        /* Soldier    */TileTypeMask.Empty | TileTypeMask.Bomb | TileTypeMask.Base,
        /* Bomb       */TileTypeMask.Empty, // detonation
        /* Base       */TileTypeMask.None, // type-terminal; owner still mutable
        /* Blocked    */TileTypeMask.None,
        /* Motherload */TileTypeMask.None, // type-terminal; owner change wins the game
    };

    // The subset a client RPC may ask for.
    // Everything else is rule-driven and only ever produced by the server, so a malformed packet requesting
    // Bomb => Empty (a free detonation) is rejected at the cheap gate.
    private static readonly TileTypeMask[] Requestable =
    {
        /* None       */TileTypeMask.None,
        /* Empty      */TileTypeMask.Soldier, // move a soldier in
        /* Soldier    */TileTypeMask.Bomb | TileTypeMask.Base, // place bomb, build base
        /* Bomb       */TileTypeMask.None,
        /* Base       */TileTypeMask.None,
        /* Blocked    */TileTypeMask.None,
        /* Motherload */TileTypeMask.None,
    };

    // Fails loudly at startup rather than throwing a TypeInitializationException at every cell site later.
    static TileTransitions()
    {
        var count = Enum.GetValues(typeof(TileType)).Length;

        if (Allowed.Length != count || Requestable.Length != count)
            throw new InvalidOperationException(
                $"TileTransitions tables ({Allowed.Length}/{Requestable.Length}) out of sync with TileType ({count}).");

        // Guards against a new TileType overflowing TileTypeMask's backing type.
        if (count > 16)
            throw new InvalidOperationException(
                $"TileType has {count} values; TileTypeMask (ushort) holds 16.");

        for (var i = 0; i < count; i++)
            if ((Requestable[i] & ~Allowed[i]) != 0)
                throw new InvalidOperationException(
                    $"Requestable[{(TileType)i}] permits transitions Allowed does not.");
    }

    private static TileTypeMask MaskOf(TileType t) => (TileTypeMask)(1 << (int)t);

    // Pipeline gate 3a. One array read.
    // Checks if the transition is legal at all, no matter what triggers it, used for both server and player initiated transitions.
    // Bomb -> Empty is a legal transition here. 
    public static bool CanBecome(TileType from, TileType to)
        => (Allowed[(int)from] & MaskOf(to)) != 0;

    // Pipeline gate 3b. Rejects well-formed but illegitimate intents.
    // Only player-initiated requests, check if a client's RPC ask for this
    // Bomb -> Empty is NOT a legal transition here. 
    public static bool CanRequest(TileType from, TileType to)
        => (Requestable[(int)from] & MaskOf(to)) != 0;
}

// What one specific player is allowed to know about a cell.
// This is what goes over the wire. TileState never does.
public readonly struct TileView
{
    public readonly TileType VisualType; // Enemy Bomb reads as Soldier
    public readonly byte OwnerId;
    public readonly bool Frozen; // render hint; derived server-side each mutation

    public TileView(TileType visualType, byte ownerId, bool frozen)
    {
        VisualType = visualType;
        OwnerId = ownerId;
        Frozen = frozen;
    }
    
    public bool IsUnknown => VisualType == TileType.None;
    public bool HasOwner => OwnerId != TileState.NoOwner;
    
    // Mirrors TileState.ConductsConnectivity so the client can derive legal moves from projected data without a second copy of the rule.
    public bool ConductsConnectivity
        => VisualType is TileType.Soldier or TileType.Bomb or TileType.Base or TileType.Motherload;
    
    public bool IsFormationUnity => VisualType is TileType.Soldier or TileType.Bomb;
    public bool IsFormationCore => VisualType is TileType.Soldier;
}

// The single point at which hidden information is filtered.
// SECURITY: this MUST run server-side, before serialization.
// Sending TileState and masking bombs inside BoardView would leak every bomb to anyone reading packets or process memory.
public static class TileProjector
{
    public static TileView Project(in TileState state, byte viewerId, bool frozen)
    {
        var hidden = state.Type is TileType.Bomb && state.OwnerId != viewerId;
        return new TileView(hidden ? TileType.Soldier : state.Type, state.OwnerId, frozen);
    }
}
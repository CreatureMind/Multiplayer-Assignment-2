using Fusion;

/// <summary>
/// Server-authoritative description of a room, replicated to clients through the
/// Lobby Hub registry. Kept deliberately small: modes/maps are byte ids and the
/// session name is derived from RoomId, so no long strings live in networked state.
/// </summary>
public struct RoomInfo : INetworkStruct
{
    public NetworkString<_8> SessionName;
    public int RoomId;                     // server-assigned; session name derives from this
    public NetworkString<_16> DisplayName; // human friendly name shown in the UI
    public byte ModeId;                    // index into LobbyCatalog.Modes
    public byte MapId;                     // index into LobbyCatalog.Maps
    public byte PlayerCount;
    public byte MaxPlayers;
    public NetworkBool IsOpen;             // waiting for players (true) vs match in progress (false)
    public NetworkBool IsPublic;
    public PlayerRef Owner;

    public bool IsFull => PlayerCount >= MaxPlayers;
    public string ModeName => LobbyCatalog.ModeName(ModeId);
    public string MapName => LobbyCatalog.MapName(MapId);
    public string RoomCode => SessionName.Value;
}

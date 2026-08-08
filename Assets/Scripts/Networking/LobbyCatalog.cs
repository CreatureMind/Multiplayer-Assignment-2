using System;

/// <summary>
/// Shared, non-networked lookup for room metadata. Modes/maps travel over the
/// network as small byte ids (indices into these arrays) instead of strings, to
/// keep the replicated RoomInfo tiny. Keep these in sync with the UXML dropdowns.
/// </summary>
public static class LobbyCatalog
{
    private static readonly string[] Modes = { "Free for All", "2 vs 2" };
    private static readonly string[] Maps  = { "Basic", "Plus", "Chokepoint" };

    public static byte ModeId(string name)
    {
        var i = Array.IndexOf(Modes, name);
        return (byte)(i < 0 ? 0 : i);
    }

    public static string ModeName(int id) => id >= 0 && id < Modes.Length ? Modes[id] : "Unknown";

    public static byte MapId(string name)
    {
        var i = Array.IndexOf(Maps, name);
        return (byte)(i < 0 ? 0 : i);
    }

    public static string MapName(int id) => id >= 0 && id < Maps.Length ? Maps[id] : "Unknown";
}
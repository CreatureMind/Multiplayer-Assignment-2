using System.Text;

/// <summary>
/// Packs the data a client must hand the server at connection time into the
/// StartGameArgs.ConnectionToken byte[]. Format: "displayName|ownerToken".
/// ownerToken is empty when simply joining an existing room.
/// </summary>
public static class ConnectionTokenUtils
{
    private const char Separator = '|';

    public static byte[] Encode(string displayName, string ownerToken = "")
    {
        var payload = $"{displayName}{Separator}{ownerToken}";
        return Encoding.UTF8.GetBytes(payload);
    }

    public static void Decode(byte[] token, out string displayName, out string ownerToken)
    {
        displayName = string.Empty;
        ownerToken = string.Empty;

        if (token == null || token.Length == 0)
            return;

        var payload = Encoding.UTF8.GetString(token);
        var parts = payload.Split(Separator);

        if (parts.Length > 0) displayName = parts[0];
        if (parts.Length > 1) ownerToken = parts[1];
    }
}

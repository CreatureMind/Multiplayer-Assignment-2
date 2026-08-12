using Fusion;
using UnityEngine;

public static class GameTraceLogger
{
    private const string MovePrefix = "<color=#81C784>[MOVE]</color>";
    private const string TurnPrefix = "<color=#64B5F6>[TURN]</color>";
    private const string BoardPrefix = "<color=#FFB74D>[BOARD]</color>";
    private const string RpcPrefix = "<color=#FFD166>[RPC]</color>";
    private const string HandshakePrefix = "<color=#4DD0E1>[HANDSHAKE]</color>";

    public static void Move(NetworkBool enabled, string message) => Log(enabled, MovePrefix, message);
    public static void Turn(NetworkBool enabled, string message) => Log(enabled, TurnPrefix, message);
    public static void Board(NetworkBool enabled, string message) => Log(enabled, BoardPrefix, message);
    public static void Rpc(NetworkBool enabled, string message) => Log(enabled, RpcPrefix, message);
    public static void Handshake(NetworkBool enabled, string message) => Log(enabled, HandshakePrefix, message);

    public static void Move(bool enabled, string message) => Log(enabled, MovePrefix, message);
    public static void Turn(bool enabled, string message) => Log(enabled, TurnPrefix, message);
    public static void Board(bool enabled, string message) => Log(enabled, BoardPrefix, message);
    public static void Rpc(bool enabled, string message) => Log(enabled, RpcPrefix, message);
    public static void Handshake(bool enabled, string message) => Log(enabled, HandshakePrefix, message);

    private static void Log(NetworkBool enabled, string prefix, string message)
    {
        if (!enabled)
            return;

        Debug.Log($"{prefix} {message}");
    }

    private static void Log(bool enabled, string prefix, string message)
    {
        if (!enabled)
            return;

        Debug.Log($"{prefix} {message}");
    }
}

using Fusion;
using UnityEngine;

/// <summary>
/// Server-side helper that broadcasts "System" chat messages via a ChatRelay on the same runner.
/// This exists because dedicated server builds don't run the client-side ChatNetworkManager.
/// </summary>
public class RunnerChatAnnouncer : MonoBehaviour
{
    private const string SYSTEM = "System";
    private const string ALL = "All";

    private ChatRelay _relay;
    private int _seq;

    public void SetRelay(ChatRelay relay) => _relay = relay;

    public void AnnounceJoined(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return;
        Send($"{displayName} joined.");
    }

    public void AnnounceLeft(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return;
        Send($"{displayName} left.");
    }

    public void AnnounceReady(string displayName, bool ready)
    {
        if (string.IsNullOrEmpty(displayName)) return;
        Send(ready ? $"{displayName} is now ready." : $"{displayName} is now not ready!");
    }

    private void Send(string text)
    {
        if (!_relay) return;
        if (!_relay.HasStateAuthority) return;

        var msg = new MessageData(SYSTEM, ALL, text)
        {
            SenderId = 0,
            Seq = _seq++
        };

        _relay.RPC_SendMessage(msg);
    }
}


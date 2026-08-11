using Events;
using UnityEngine;

public sealed class TurnEndChatAnnouncer : MonoBehaviour
{
    private const string SYSTEM = "System";
    private const string ALL = "All";
    
    private bool _wasMyTurn;
    
    private void OnEnable()
        => EventBus.Subscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);

    private void OnDisable()
        => EventBus.Unsubscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);

    private void OnTurnStateChanged(LocalTurnStateChangedEvent e)
    {
        if (_wasMyTurn && !e.IsMyTurn)
            Announce();
        
        _wasMyTurn = e.IsMyTurn;
    }

    private void Announce()
    {
        var playerName = ResolveLocalName();
        if (string.IsNullOrEmpty(playerName))
            playerName = "A player";

        EventBus.Raise(new ChatMessageEvent
            {
                Sender = SYSTEM,
                Target = ALL,
                Message = $"{playerName} has ended their turn."
            }
        );
    }
    
    private static string ResolveLocalName()
    {
        var nm = NetworkManager.Instance;
        if (!nm)
            return string.Empty;
        
        if (!string.IsNullOrEmpty(nm.LocalConfirmedName))
            return nm.LocalConfirmedName;

        var data = nm.GetLocalPlayerData();
        return data ? data.DisplayName.ToString() : string.Empty;
    }
}
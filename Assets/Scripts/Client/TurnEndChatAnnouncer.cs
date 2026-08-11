using Events;
using UnityEngine;

public sealed class TurnEndChatAnnouncer : MonoBehaviour
{
    private const string ALL = "All";
    
    private bool _wasMyTurn;
    
    private void OnEnable()
    {
        EventBus.Subscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);
        Debug.Log("[TurnEndAnnouncer] OnEnable — subscribed");
    }

    private void OnDisable()
        => EventBus.Unsubscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);

    private void OnTurnStateChanged(LocalTurnStateChangedEvent e)
    {
        Debug.Log($"[TurnEndAnnouncer] snapshot IsMyTurn={e.IsMyTurn} (was {_wasMyTurn})");
        
        if (_wasMyTurn && !e.IsMyTurn)
            Announce();
        
        _wasMyTurn = e.IsMyTurn;
    }

    private void Announce()
    {
        EventBus.Raise(new ChatMessageEvent
            {
                Sender = GetLocalPlayerName(),
                Target = ALL,
                Message = $"I end my turn."
            }
        );
    }
    
    private string GetLocalPlayerName()
    {
        if (!NetworkManager.Instance)
            return string.Empty;
        
        if (!string.IsNullOrEmpty(NetworkManager.Instance.LocalConfirmedName))
            return NetworkManager.Instance.LocalConfirmedName;
        var data = NetworkManager.Instance.GetLocalPlayerData();
        return data ? data.DisplayName.ToString() : string.Empty;
    }
}
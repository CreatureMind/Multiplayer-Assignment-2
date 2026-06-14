using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;

public struct MessageData : INetworkStruct
{
    public NetworkString<_32> Sender;
    public NetworkString<_32> Target;
    public NetworkString<_32> Message;

    public MessageData(string sender, string target, string message)
    {
        Sender = sender;
        Target = target;
        Message = message;
    }
}

public class ChatNetworkManager : MonoBehaviour
{
    private readonly Queue<MessageData> _chatHistory = new();
    private const int CHAT_MAX_HISTORY = 200;
    
    // Message types
    private const string ALL = "All";
    private const string SYSTEM = "System";

    public ChatRelay ChatRelay { get; set; }

    private void OnEnable()
    {
        EventBus.Subscribe<ChatMessageEvent>(OnChatMessageSubmitted);
        EventBus.Subscribe<NetworkMessageReceivedEvent>(OnNetworkMessageReceived);
        EventBus.Subscribe<ChatCreatedEvent>(LoadChatHistory);
        EventBus.Subscribe<HistoryRequestedEvent>(OnHistoryRequested);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChatMessageEvent>(OnChatMessageSubmitted);
        EventBus.Unsubscribe<NetworkMessageReceivedEvent>(OnNetworkMessageReceived);
        EventBus.Unsubscribe<ChatCreatedEvent>(LoadChatHistory);
        EventBus.Unsubscribe<HistoryRequestedEvent>(OnHistoryRequested);
    }

    private void OnHistoryRequested(HistoryRequestedEvent e)
    {
        if (!ChatRelay) return;
        foreach (var message in _chatHistory)
            ChatRelay.RPC_SendHistoryEntry(e.Requester, message);
    }

    private void OnChatMessageSubmitted(ChatMessageEvent e)
    {
        if (!ChatRelay)
        {
            Debug.LogError("ChatRelay is null");
            return;
        }

        var message = new MessageData(e.Sender, e.Target, e.Message);

        if (e.Target == ALL)
        {
            // All-chat echoes back to the sender, so don't render locally
            ChatRelay.RPC_SendMessage(message);
            return;
        }

        // Whisper: target won't echo to the sender, so render+save locally
        if (!TryGetPlayerRefByName(e.Target, out var targetRef))
        {
            Debug.LogWarning($"Whisper target '{e.Target}' not found");
            return;
        }

        SaveAndRender(message);
        ChatRelay.RPC_SendWhisper(targetRef, message);
    }

    private void OnNetworkMessageReceived(NetworkMessageReceivedEvent e)
    {
        SaveAndRender(new MessageData(e.Sender, e.Target, e.Message));
    }

    private void LoadChatHistory(ChatCreatedEvent e)
    {
        foreach (var message in _chatHistory)
            Render(message);
    }

    private void SaveAndRender(MessageData message)
    {
        _chatHistory.Enqueue(message);
        if (_chatHistory.Count > CHAT_MAX_HISTORY)
            _chatHistory.Dequeue();

        Render(message);
    }

    private void Render(MessageData message)
    {
        EventBus.Raise(new OnMessageReceivedEvent
        {
            MessageType = GetMessageType(message.Target.Value, message.Sender.Value),
            Sender = message.Sender.Value,
            Target = message.Target.Value,
            Message = message.Message.Value
        });
    }

    private MessageType GetMessageType(string target, string sender)
    {
        if (target is ALL && sender is SYSTEM)
            return MessageType.System;

        if (target is ALL)
            return MessageType.All;

        var localName = GetLocalPlayerName();

        if (sender == localName)
            return MessageType.WhisperTo;

        if (target == localName)
            return MessageType.WhisperFrom;

        Debug.LogWarning($"ChatNetworkManager: unresolved message from {sender} to {target}");
        return MessageType.System;
    }

    private string GetLocalPlayerName()
    {
        var data = NetworkManager.Instance ? NetworkManager.Instance.GetLocalPlayerData() : null;
        return data ? data.DisplayName.ToString() : string.Empty;
    }

    private bool TryGetPlayerRefByName(string displayName, out PlayerRef playerRef)
    {
        foreach (var data in NetworkManager.Instance.GetAllPlayers())
        {
            if (data.DisplayName.ToString() != displayName) continue;
            playerRef = data.Object.InputAuthority;
            return true;
        }
        playerRef = default;
        return false;
    }
}

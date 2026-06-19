using System;
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

    private readonly Dictionary<PlayerRef, string> _trackedNames = new();
    private readonly HashSet<PlayerRef> _joinedAnnounced = new();
    private readonly HashSet<PlayerRef> _readyAnnounced = new();

    private ChatRelay _chatRelay;
    public ChatRelay ChatRelay
    {
        get => _chatRelay;
        set
        {
            _chatRelay = value;
            if (_chatRelay && _chatRelay.HasStateAuthority)
                SweepPlayers();
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ChatMessageEvent>(OnChatMessageSubmitted);
        EventBus.Subscribe<NetworkMessageReceivedEvent>(OnNetworkMessageReceived);
        EventBus.Subscribe<ChatCreatedEvent>(LoadChatHistory);
        EventBus.Subscribe<ChatHistoryRequestedEvent>(OnHistoryRequested);
        EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        EventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChatMessageEvent>(OnChatMessageSubmitted);
        EventBus.Unsubscribe<NetworkMessageReceivedEvent>(OnNetworkMessageReceived);
        EventBus.Unsubscribe<ChatCreatedEvent>(LoadChatHistory);
        EventBus.Unsubscribe<ChatHistoryRequestedEvent>(OnHistoryRequested);
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        EventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
    }

    private void OnHistoryRequested(ChatHistoryRequestedEvent e)
    {
        if (!ChatRelay) return;
        var requesterName = GetPlayerName(e.Requester);
        foreach (var message in _chatHistory)
        {
            if (!IsVisibleTo(message, requesterName)) continue;
            ChatRelay.RPC_SendHistoryEntry(e.Requester, message);
        }
    }

    private static bool IsVisibleTo(MessageData message, string viewerName)
    {
        var target = message.Target.Value;
        if (target == ALL) return true;
        if (message.Sender.Value == viewerName) return true;
        return target == viewerName;
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

    private void OnPlayerListChanged(PlayerListChangedEvent _)
    {
        var currentRefs = new HashSet<PlayerRef>();
        foreach (var data in NetworkManager.Instance.GetAllPlayers())
            currentRefs.Add(data.Object.InputAuthority);

        var left = new List<PlayerRef>();
        foreach (var kv in _trackedNames)
            if (!currentRefs.Contains(kv.Key)) left.Add(kv.Key);

        foreach (var playerRef in left)
        {
            if (HasChatStateAuthority())
                BroadcastSystem($"{_trackedNames[playerRef]} left.");
            _trackedNames.Remove(playerRef);
            _joinedAnnounced.Remove(playerRef);
            _readyAnnounced.Remove(playerRef);
        }

        SweepPlayers();
    }

    private void OnPlayerDataChanged(PlayerDataChangedEvent e)
    {
        ProcessPlayer(FindPlayer(e.PlayerRef));
    }

    private void OnMatchStarted(MatchStartedEvent _)
    {
        if (!HasChatStateAuthority()) return;
        BroadcastSystem("Game starting!");
    }

    private void SweepPlayers()
    {
        if (NetworkManager.Instance == null) return;
        foreach (var data in NetworkManager.Instance.GetAllPlayers())
            ProcessPlayer(data);
    }

    private void ProcessPlayer(PlayerData data)
    {
        if (!data) return;
        var playerRef = data.Object.InputAuthority;
        var displayName = data.DisplayName.ToString();
        if (string.IsNullOrEmpty(displayName)) return;

        _trackedNames[playerRef] = displayName;

        if (!_joinedAnnounced.Contains(playerRef) && !displayName.StartsWith("Player_"))
        {
            _joinedAnnounced.Add(playerRef);
            if (HasChatStateAuthority())
                BroadcastSystem($"{displayName} joined.");
        }

        if (data.IsReady)
        {
            if (_readyAnnounced.Add(playerRef) && HasChatStateAuthority())
                BroadcastSystem($"{displayName} is now ready.");
        }
        else
        {
            if (_readyAnnounced.Remove(playerRef) && HasChatStateAuthority())
                BroadcastSystem($"{displayName} is now not ready!");
        }
    }

    private bool HasChatStateAuthority() => _chatRelay && _chatRelay.HasStateAuthority;

    private void BroadcastSystem(string text)
    {
        if (!_chatRelay) return;
        _chatRelay.RPC_SendMessage(new MessageData(SYSTEM, ALL, text));
    }

    private PlayerData FindPlayer(PlayerRef playerRef)
    {
        foreach (var data in NetworkManager.Instance.GetAllPlayers())
            if (data.Object.InputAuthority == playerRef) return data;
        return null;
    }

    private string GetPlayerName(PlayerRef playerRef)
    {
        var data = FindPlayer(playerRef);
        return data ? data.DisplayName.ToString() : string.Empty;
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

    internal void ResetSessionState()
    {
        _chatHistory.Clear();
        _trackedNames.Clear();
        _joinedAnnounced.Clear();
        _readyAnnounced.Clear();
    }
}

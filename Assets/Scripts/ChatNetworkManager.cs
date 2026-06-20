using System;
using System.Collections.Generic;
using Events;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MessageData : INetworkStruct
{
    public NetworkString<_32> Sender;
    public NetworkString<_32> Target;
    public NetworkString<_32> Message;
    public int SenderId;
    public int Seq;

    public MessageData(string sender, string target, string message)
    {
        Sender = sender;
        Target = target;
        Message = message;
        SenderId = 0;
        Seq = 0;
    }
}

public class ChatNetworkManager : MonoBehaviour
{
    private readonly Queue<MessageData> _chatHistory = new();
    private const int CHAT_MAX_HISTORY = 200;

    // Message types
    private const string ALL = "All";
    private const string SYSTEM = "System";

    private const string GAME_SCENE = "Game_Scene";

    private readonly Dictionary<PlayerRef, string> _trackedNames = new();
    private readonly HashSet<PlayerRef> _joinedAnnounced = new();
    private readonly HashSet<PlayerRef> _readyAnnounced = new();

    // Per-message identity used to drop duplicates that arrive both as a live
    // broadcast and as a replayed history entry during the join window.
    private readonly HashSet<long> _seenMessages = new();
    private int _outSeq;

    // While true (during a match/scene transition) presence announcements are
    // muted so despawning/respawning players don't spam join/ready/left lines.
    private bool _suppressAnnouncements;

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
        EventBus.Subscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
        EventBus.Subscribe<SceneLoadDoneEvent>(OnSceneLoadDone);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChatMessageEvent>(OnChatMessageSubmitted);
        EventBus.Unsubscribe<NetworkMessageReceivedEvent>(OnNetworkMessageReceived);
        EventBus.Unsubscribe<ChatCreatedEvent>(LoadChatHistory);
        EventBus.Unsubscribe<ChatHistoryRequestedEvent>(OnHistoryRequested);
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        EventBus.Unsubscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
        EventBus.Unsubscribe<SceneLoadDoneEvent>(OnSceneLoadDone);
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

        var message = NewMessage(e.Sender, e.Target, e.Message);

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
        SaveAndRender(e.Message);
    }

    private void LoadChatHistory(ChatCreatedEvent e)
    {
        foreach (var message in _chatHistory)
            Render(message);
    }

    private void SaveAndRender(MessageData message)
    {
        // Drop a message we've already shown (live broadcast + history replay race).
        var key = ((long)message.SenderId << 32) | (uint)message.Seq;
        if (!_seenMessages.Add(key)) return;

        // System lines (joined/ready/left/game-starting) are live-only: they are
        // never replayed to late joiners, only real chat is kept in history.
        if (message.Sender.Value != SYSTEM)
        {
            _chatHistory.Enqueue(message);
            if (_chatHistory.Count > CHAT_MAX_HISTORY)
                _chatHistory.Dequeue();
        }

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
        if (PresenceMuted()) return;

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
        if (PresenceMuted()) return;
        ProcessPlayer(FindPlayer(e.PlayerRef));
    }

    private void OnSceneLoadStarted(SceneLoadStartedEvent _)
    {
        // Fires before any object despawns, while the lobby is still the active
        // scene. Mute presence spam for the unload window (PresenceMuted() takes
        // over via the scene check once the game scene is active). "Game
        // starting!" is rendered locally on each client because the relay is
        // about to despawn and an RPC racing the scene load would be dropped.
        _suppressAnnouncements = true;
        RenderLocalSystem("Game starting!");
    }

    // Game scene is now active; the scene check in PresenceMuted() keeps presence
    // announcements muted from here, so the unload-window flag can be released.
    private void OnSceneLoadDone(SceneLoadDoneEvent _) => _suppressAnnouncements = false;

    // Presence announcements (joined/ready/left) are a lobby-only feature. Mute
    // them in the game scene and during the transition's unload window.
    private bool PresenceMuted()
        => _suppressAnnouncements || SceneManager.GetActiveScene().name == GAME_SCENE;

    private void RenderLocalSystem(string text)
    {
        EventBus.Raise(new OnMessageReceivedEvent
        {
            MessageType = MessageType.System,
            Sender = SYSTEM,
            Target = ALL,
            Message = text
        });
    }

    private void SweepPlayers()
    {
        if (NetworkManager.Instance == null) return;
        foreach (var data in NetworkManager.Instance.GetAllPlayers())
            ProcessPlayer(data);
    }

    private void ProcessPlayer(PlayerData data)
    {
        if (PresenceMuted()) return;
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
        _chatRelay.RPC_SendMessage(NewMessage(SYSTEM, ALL, text));
    }

    // Stamps each outgoing message with a unique (senderId, seq) identity so
    // receivers can dedup a live broadcast against its history replay.
    private MessageData NewMessage(string sender, string target, string text)
    {
        var senderId = NetworkManager.Instance ? NetworkManager.Instance.LocalPlayer.PlayerId : 0;
        return new MessageData(sender, target, text) { SenderId = senderId, Seq = _outSeq++ };
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
        _seenMessages.Clear();
        _outSeq = 0;
        _suppressAnnouncements = false;
    }
}

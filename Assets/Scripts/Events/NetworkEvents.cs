using System.Collections.Generic;
using Fusion;

namespace Events
{
    public struct JoinedLobbyEvent { }

    public struct RoomCreatedEvent
    {
        public string RoomName;
    }

    public struct SessionDataRefreshedEvent
    {
        public List<SessionInfo> Sessions;
        public int TotalPlayers;
    }

    public struct ShowLoadingScreenEvent { }

    public struct HideLoadingScreenEvent { }
    
    public struct PlayerListChangedEvent { }

    public struct OnPlayerListChangedEvent
    {
        public List<string> PlayerNames;
    }

    public struct PlayerDataChangedEvent 
    { 
        public PlayerRef PlayerRef;
    }
    
    public struct MatchStartedEvent { }

    // Raised on every client when a networked scene load begins, before any
    // network objects are despawned.
    public struct SceneLoadStartedEvent { }

    // Raised on every client once a networked scene load has finished.
    public struct SceneLoadDoneEvent { }
    
    public struct ChatCreatedEvent { }

    public struct ChatMessageEvent
    {
        public string Sender;
        public string Target;
        public string Message;
    }
    
    public struct OnMessageReceivedEvent
    {
        public MessageType MessageType;
        public string Sender;
        public string Target;
        public string Message;
    }

    public struct NetworkMessageReceivedEvent
    {
        public MessageData Message;
    }

    public struct ChatHistoryRequestedEvent
    {
        public PlayerRef Requester;
    }
    
    public struct OnChatRelaySpawnedEvent{}
    
    public struct OnChatRelayDespawnedEvent{}
    
    public struct CharacterClaimedEvent
    {
        public int CharacterId;
        public PlayerRef ClaimedBy;
    }

    public struct CharacterReleasedEvent
    {
        public int CharacterId;
    }

    public struct CharacterSelectionConfirmedEvent
    {
        public int CharacterId;
    }

    public struct CharacterSelectionDeniedEvent
    {
        public int CharacterId;
    }

    public struct PlayerNameConfirmedEvent
    {
        public string PlayerName;
    }
    
    public struct CharacterSelectionManagerReadyEvent { }
}
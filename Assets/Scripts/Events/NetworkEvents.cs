using System.Collections.Generic;
using Fusion;

namespace Events
{
    public struct JoinedLobbyEvent { }

    public struct RoomCreatedEvent
    {
        public string RoomName;
        //Assignment 3
        public string ModeName;
        public string MapName;
    }

    public struct SessionDataRefreshedEvent
    {
        public List<SessionInfo> Sessions;
        public int TotalPlayers;
    }

    // Server-driven room list (replaces SessionDataRefreshedEvent as the lobby data source).
    public struct RoomListChangedEvent
    {
        public List<RoomInfo> Rooms;
        public int TotalPlayers;
    }

    // Raised on the requesting client when the server refuses a create/join.
    public struct RoomJoinRejectedEvent
    {
        public string Reason;
    }

    public struct ShowLoadingScreenEvent { }

    public struct HideLoadingScreenEvent { }
    
    public struct PlayerListChangedEvent { }

    public struct PlayerLeftEvent
    {
        public PlayerRef Player;
    }

    public struct OnPlayerListChangedEvent
    {
        public List<string> PlayerNames;
    }

    public struct PlayerDataChangedEvent 
    { 
        public PlayerRef PlayerRef;
    }
    
    public struct MatchStartedEvent { }


    public struct SceneLoadStartedEvent { }
    
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
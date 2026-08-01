using System.Collections.Generic;
using Fusion;

namespace Events
{
    public struct JoinedLobbyEvent { }

    public struct RoomCreatedEvent
    {
        public string RoomName;
        public string ModeName;
        public string MapName;
    }

    public struct RoomListChangedEvent
    {
        public List<RoomInfo> Rooms;
        public int TotalPlayers;
    }
    
    public struct JoinedRoomEvent { }

    // Raised on the requesting client when the server refuses a creation/join.
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

    public struct GameSceneLoadedEvent { }

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
    

    public struct PlayerNameConfirmedEvent
    {
        public string PlayerName;
    }
    
    public struct PlaySoundEvent
    {
        public SoundEffectEnum SoundName;
    }
}
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
        public string Sender;
        public string Target;
        public string Message;
    }

    public struct HistoryRequestedEvent
    {
        public PlayerRef Requester;
    }
    
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
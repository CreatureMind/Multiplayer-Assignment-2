using System;
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
        public bool IsPublic;
        public string RoomCode;
    }

    public struct RoomListChangedEvent
    {
        public List<RoomInfo> Rooms;
        public int TotalPlayers;
    }
    
    public struct JoinedRoomEvent { }

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
    
    public struct HideChatEvent {}

    public struct PlayerNameConfirmedEvent
    {
        public string PlayerName;
    }
    
    public struct ReturnToMainMenuEvent {}
    
    public struct OpenRoomCreationOverlayEvent {}
    
    // Audio system
    public struct PlaySoundEvent
    {
        public SoundEffectEnum SoundName;
    }
    
    // Feedback Dialog
    public enum DialogType
    {
        Info,
        Warning,
        Error
    }

    public struct ShowDialogEvent
    {
        public readonly string Title;
        public readonly string Message;
        public readonly DialogType Type;

        public readonly string PrimaryText;
        public readonly Action OnPrimary;

        public readonly string SecondaryText;
        public readonly Action OnSecondary;

        public readonly string TertiaryText;
        public readonly Action OnTertiary;

        public ShowDialogEvent(
            string title,
            string message,
            string primaryText = "OK", Action onPrimary = null,
            string secondaryText = null, Action onSecondary = null,
            string tertiaryText = null, Action onTertiary = null,
            DialogType type = DialogType.Error)
        {
            Title = title;
            Message = message;
            Type = type;

            PrimaryText = primaryText;
            OnPrimary = onPrimary;

            SecondaryText = secondaryText;
            OnSecondary = onSecondary;

            TertiaryText = tertiaryText;
            OnTertiary = onTertiary;
        }
    }
}
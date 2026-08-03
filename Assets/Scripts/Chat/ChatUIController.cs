using System;
using System.Collections.Generic;
using Events;
using UnityEngine;
using UnityEngine.UIElements;

public enum MessageType { 
    System,
    All, 
    WhisperFrom, 
    WhisperTo 
}

public class ChatUIController : MonoBehaviour
{
    private UIDocument    _document;
    private VisualElement _root;
    
    private ScrollView    _chatScrollView;
    private TextField     _chatTextField;
    private DropdownField _chatDropdown;
    private VisualElement _chatContainer;

    private Button _chatBtn;

    private string _currentTarget = ALL_OPTION;
    private const string ALL_OPTION = "All";
    
    //class names
    private const string CHAT_HIDDEN = "chat--hidden";
    private const string CHAT_MSG = "chat-msg";
    private const string CHAT_MSG_PREFIX = "chat-msg__prefix";
    private const string CHAT_MSG_BODY = "chat-msg__body";
    private const string CHAT_MSG_PREFIX_SYSTEM = "chat-msg__prefix--system";
    private const string CHAT_MSG_PREFIX_ALL = "chat-msg__prefix--all";
    private const string CHAT_MSG_PREFIX_WHISPER_FROM = "chat-msg__prefix--whisper-from";
    private const string CHAT_MSG_PREFIX_WHISPER_TO = "chat-msg__prefix--whisper-to";

    private bool _isVisible = true;
    
    private void OnEnable()
    {
        EventBus.Subscribe<OnMessageReceivedEvent>   (RenderMessage);
        EventBus.Subscribe<PlayerListChangedEvent>   (OnPlayerListChanged);
        EventBus.Subscribe<PlayerDataChangedEvent>   (OnPlayerDataChanged);
        EventBus.Subscribe<OnChatRelayDespawnedEvent>(OnChatRelayDespawned);
        EventBus.Subscribe<PlayerNameConfirmedEvent> (Show);
        EventBus.Subscribe<ReturnToMainMenuEvent>    (Hide);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnMessageReceivedEvent>   (RenderMessage);
        EventBus.Unsubscribe<PlayerListChangedEvent>   (OnPlayerListChanged);
        EventBus.Unsubscribe<PlayerDataChangedEvent>   (OnPlayerDataChanged);
        EventBus.Unsubscribe<OnChatRelayDespawnedEvent>(OnChatRelayDespawned);
        EventBus.Unsubscribe<PlayerNameConfirmedEvent> (Show);
        EventBus.Unsubscribe<ReturnToMainMenuEvent>    (Hide);

        _chatTextField?.UnregisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
    }

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        if (!_document)
        {
            Debug.LogError("[ChatUIController] UIDocument is null!");
            return;
        }
        
        InitializeUI(_document);
        
        EventBus.Raise(new ChatCreatedEvent());
        DontDestroyOnLoad(gameObject);
    }

    private void InitializeUI(UIDocument document)
    {
        _root = document.rootVisualElement;

        _chatScrollView = _root.Q<ScrollView>  (UI_Chat_View.chat_scroll_view);
        _chatScrollView.Clear();
        
        _chatTextField = _root.Q<TextField>    (UI_Chat_View.text_field);
        _chatDropdown  = _root.Q<DropdownField>(UI_Chat_View.dropdown_field);
        _chatContainer = _root.Q<VisualElement>(UI_Chat_View.chat_container);
        _chatBtn       = _root.Q<Button>       (UI_Chat_View.chat_btn);
        
        RefreshPlayerDropdown();
        
        SetupCallbacks();
    }
    
    private void SetupCallbacks()
    {
        if (_chatBtn != null)
        {
            _chatBtn.clicked += () => _chatContainer.ToggleInClassList(CHAT_HIDDEN);
        }
        else
        {
            Debug.LogError("[ChatUIController] Could not find Button named 'chat-btn' in Chat_View.");
        }

        _chatTextField.RegisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
        _chatScrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollToBottom);
        _chatDropdown.RegisterValueChangedCallback(OnDropDownValueChanged);
    }

    private void OnPlayerListChanged(PlayerListChangedEvent _) => RefreshPlayerDropdown();
    private void OnPlayerDataChanged(PlayerDataChangedEvent _) => RefreshPlayerDropdown();
    
    private void RefreshPlayerDropdown()
    {
        var playerNames = new List<string>();
        if (NetworkManager.Instance)
        {
            foreach (var p in NetworkManager.Instance.GetAllPlayers())
            {
                if (p == null || !p.Object || !p.Object.IsValid) continue;
                var displayName = p.DisplayName.ToString();
                if (!string.IsNullOrEmpty(displayName))
                    playerNames.Add(displayName);
            }
        }
        UpdatePlayerDropdown(playerNames);
    }

    private void UpdatePlayerDropdown(List<string> playerNames)
    {
        if (_chatDropdown == null) return;

        var currentlySelectedValue = _chatDropdown.value;

        List<string> displayChoices = new() { ALL_OPTION };

        foreach (var playerName in playerNames)
        {
            if (playerName == GetLocalPlayerName())
                continue;

            displayChoices.Add(playerName);
        }

        _chatDropdown.choices = displayChoices;

        if (displayChoices.Contains(currentlySelectedValue))
            _chatDropdown.value = currentlySelectedValue;
        else
            _chatDropdown.value = ALL_OPTION;
    }

    private void FocusChatField()
    {
        _chatTextField.schedule.Execute(() =>
        {
            var input = _chatTextField.Q(TextField.textInputUssName);
            if (input != null)
                input.Focus();
            else
                _chatTextField.Focus();
        });
    }

    private void OnTextFieldKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
        {
            // Stop UI Toolkit from adding a literal new line (\n) into the text box
            evt.StopPropagation();

            SubmitMessage();
        }
    }

    private void OnDropDownValueChanged(ChangeEvent<string> evt)
    {
        _currentTarget = evt.newValue;
        FocusChatField();
    }

    private void SubmitMessage()
    {
        var message = _chatTextField.value.Trim();
        if (string.IsNullOrEmpty(message)) return;
        
        EventBus.Raise(new ChatMessageEvent
        {
            Sender = GetLocalPlayerName(),
            Target = _currentTarget,
            Message = message
        });
        
        _chatTextField.value = string.Empty;
    }

    private void RenderMessage(OnMessageReceivedEvent e)
    {
        if (_chatScrollView == null) return;

        var row =  new VisualElement();
        row.AddToClassList(CHAT_MSG);

        var prefix = new Label();
        prefix.AddToClassList(CHAT_MSG_PREFIX);
        
        var body = new Label(e.Message);
        body.AddToClassList(CHAT_MSG_BODY);

        ApplyPrefix(prefix, e.MessageType, e.Sender, e.Target);
        
        row.Add(prefix);
        row.Add(body);
        _chatScrollView.Insert(0, row);
        
        FocusChatField();
    }

    private void ApplyPrefix(Label prefix, MessageType type, string sender, string target)
    {
        switch (type)
        {
            case MessageType.System:
                prefix.text = "System:";
                prefix.AddToClassList(CHAT_MSG_PREFIX_SYSTEM);
                break;
            case MessageType.All:
                prefix.text = $"#{sender}:";
                prefix.AddToClassList(CHAT_MSG_PREFIX_ALL);
                break;
            case MessageType.WhisperFrom:
                prefix.text = $"@From {sender}:";
                prefix.AddToClassList(CHAT_MSG_PREFIX_WHISPER_FROM);
                break;
            case MessageType.WhisperTo:
                prefix.text = $"@To {target}:";
                prefix.AddToClassList(CHAT_MSG_PREFIX_WHISPER_TO);
                break;
            default:
                prefix.text = $"#{sender}:";
                prefix.AddToClassList(CHAT_MSG_PREFIX_ALL);
                break;
        }
    }
    
    private void ScrollToBottom(GeometryChangedEvent evt)
    {
        _chatScrollView.scrollOffset = new Vector2(0, _chatScrollView.verticalScroller.highValue);
    }
    
    private string GetLocalPlayerName()
    {
        if (!NetworkManager.Instance) return string.Empty;
        
        if (!string.IsNullOrEmpty(NetworkManager.Instance.LocalConfirmedName))
            return NetworkManager.Instance.LocalConfirmedName;
        var data = NetworkManager.Instance.GetLocalPlayerData();
        return data ? data.DisplayName.ToString() : string.Empty;
    }
    
    private void OnChatRelayDespawned(OnChatRelayDespawnedEvent e)
    {
        Debug.Log("[ChatUIController] Chat relay despawned");
        Destroy(gameObject);
    }
    
    private void Show(PlayerNameConfirmedEvent e)
    {
        if (_isVisible) return;
        _isVisible = true;

        if (_root != null) _root.style.display = DisplayStyle.Flex;
    }

    private void Hide(ReturnToMainMenuEvent e)
    {
        if (!_isVisible) return;
        _isVisible = false;

        if (_root != null) _root.style.display = DisplayStyle.None;
    }
}
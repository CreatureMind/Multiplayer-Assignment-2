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
    private ScrollView _chatScrollView;
    private TextField _chatTextField;
    private DropdownField _chatDropdown;

    private string _currentTarget = ALL_OPTION;
    private const string ALL_OPTION = "All";
    
    private void OnEnable()
    {
        EventBus.Subscribe<OnMessageReceivedEvent>(RenderMessage);
        EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        EventBus.Subscribe<OnChatRelayDespawnedEvent>(OnChatRelayDespawned);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnMessageReceivedEvent>(RenderMessage);
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);
        EventBus.Unsubscribe<OnChatRelayDespawnedEvent>(OnChatRelayDespawned);

        _chatTextField?.UnregisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
    }

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        // Chat sits on top of the in-game view in the shared panel; keep its full-screen
        // root from swallowing clicks meant for the view underneath.
        root.pickingMode = PickingMode.Ignore;
        _chatScrollView = root.Q<ScrollView>(UI_Chat_View.chat_scroll_view);
        _chatScrollView.Clear();
        _chatTextField = root.Q<TextField>(UI_Chat_View.text_field);
        _chatDropdown = root.Q<DropdownField>(UI_Chat_View.dropdown_field);

        var chatContainer = root.Q<VisualElement>(UI_Chat_View.chat_container);

        var chatBtn = root.Q<Button>(UI_Chat_View.chat_btn);
        if (chatBtn != null)
        {
            chatBtn.clicked += () =>
            {
                chatContainer.ToggleInClassList("chat--hidden");
            };
        }
        
        RefreshPlayerDropdown();
        
        _chatTextField.RegisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
        _chatScrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollToBottom);
        _chatDropdown.RegisterValueChangedCallback(OnDropDownValueChanged);
        
        EventBus.Raise(new ChatCreatedEvent());
        DontDestroyOnLoad(gameObject);
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
        row.AddToClassList("chat-msg");

        var prefix = new Label();
        prefix.AddToClassList("chat-msg__prefix");
        
        var body = new Label(e.Message);
        body.AddToClassList("chat-msg__body");

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
                prefix.AddToClassList("chat-msg__prefix--system");
                break;
            case MessageType.All:
                prefix.text = $"#{sender}:";
                prefix.AddToClassList("chat-msg__prefix--all");
                break;
            case MessageType.WhisperFrom:
                prefix.text = $"@From {sender}:";
                prefix.AddToClassList("chat-msg__prefix--whisper-from");
                break;
            case MessageType.WhisperTo:
                prefix.text = $"@To {target}:";
                prefix.AddToClassList("chat-msg__prefix--whisper-to");
                break;
            default:
                prefix.text = $"#{sender}:";
                prefix.AddToClassList("chat-msg__prefix--all");
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
        // Prefer the plain confirmed name: it survives scene changes, unlike the networked PlayerData.
        if (!string.IsNullOrEmpty(NetworkManager.Instance.LocalConfirmedName))
            return NetworkManager.Instance.LocalConfirmedName;
        var data = NetworkManager.Instance.GetLocalPlayerData();
        return data ? data.DisplayName.ToString() : string.Empty;
    }
    
    private void OnChatRelayDespawned(OnChatRelayDespawnedEvent e)
    {
        Debug.Log("OnChatRelayDespawned");
        Destroy(gameObject);
    }
}
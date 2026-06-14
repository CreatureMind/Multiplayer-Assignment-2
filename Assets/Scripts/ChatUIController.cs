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
        EventBus.Subscribe<OnPlayerListChangedEvent>(UpdatePlayerDropdown);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnMessageReceivedEvent>(RenderMessage);
        EventBus.Unsubscribe<OnPlayerListChangedEvent>(UpdatePlayerDropdown);
        
        _chatTextField.UnregisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
    }

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _chatScrollView = root.Q<ScrollView>("chat-scroll-view");
        _chatScrollView.Clear();
        _chatTextField = root.Q<TextField>("text-field");
        _chatDropdown = root.Q<DropdownField>("dropdown-field");
        
        var chatContainer = root.Q<VisualElement>("chat-container");
        
        var chatBtn = root.Q<Button>("chat-btn");
        if (chatBtn != null)
        {
            chatBtn.clicked += () =>
            {
                chatContainer.ToggleInClassList("chat--hidden");
            };
        }
        
        var initialNames = new List<string>();
        if (NetworkManager.Instance)
        {
            foreach (var p in NetworkManager.Instance.GetAllPlayers())
                initialNames.Add(p.DisplayName.ToString());
        }
        UpdatePlayerDropdown(new OnPlayerListChangedEvent { PlayerNames = initialNames });
        
        //_chatScrollView.contentContainer.RegisterCallback<GeometryChangedEvent>();
        _chatTextField.RegisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);
        _chatScrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollToBottom);
        _chatDropdown.RegisterValueChangedCallback(OnDropDownValueChanged);
        
        EventBus.Raise(new ChatCreatedEvent());
    }

    private void UpdatePlayerDropdown(OnPlayerListChangedEvent e)
    {
        if (_chatDropdown == null) return;

        var currentlySelectedValue = _chatDropdown.value;
        
        List<string> displayChoices = new() { ALL_OPTION };
        
        foreach (var playerName in e.PlayerNames)
        {
            if (playerName == GetLocalPlayerName())
                continue;

            displayChoices.Add(playerName);
        }

        _chatDropdown.choices = displayChoices;

        if (displayChoices.Contains(currentlySelectedValue))
        {
            _chatDropdown.value = currentlySelectedValue;
        }
        else
        {
            _chatDropdown.value = ALL_OPTION;
        }
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
        var data = NetworkManager.Instance ? NetworkManager.Instance.GetLocalPlayerData() : null;
        return data ? data.DisplayName.ToString() : string.Empty;
    }
}
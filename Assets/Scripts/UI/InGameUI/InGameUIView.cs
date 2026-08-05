using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class InGameUIView : MonoBehaviour
{
    public event Action OnLeaveButtonClicked;
    public event Action OnReturnButtonClicked;

    public static event Action<UIDocument> OnObjectLoaded;

    private UIDocument    _document;
    private VisualElement _root;

    private VisualElement _endGamePopup;
    private VisualElement _uiContainer;
    private Button        _leaveGameButton;
    private Button        _returnToLobbyButton;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        if (!_document)
        {
            Debug.LogError("[InGameUIView] UIDocument is null");
            return;
        }
        
        InitializeUI(_document);
    }

    private void InitializeUI(UIDocument document)
    {
        _root = document.rootVisualElement;

        _endGamePopup        = _root.Q<VisualElement>(UI_In_Game_View.ended_popup);
        _uiContainer         = _root.Q<VisualElement>(UI_In_Game_View.ui_container);
        _leaveGameButton     = _root.Q<Button>       (UI_In_Game_View.leave_button);
        _returnToLobbyButton = _root.Q<Button>       (UI_In_Game_View.return_button);

        SetupButtonCallbacks();
        OnObjectLoaded?.Invoke(_document);
    }

    private void SetupButtonCallbacks()
    {
        if (_leaveGameButton != null)
        {
            _leaveGameButton.clicked += () =>
            {
                OnLeaveButtonClicked?.Invoke();
                _leaveGameButton.text = "Leaving...";
                _leaveGameButton.SetEnabled(false);
            };
        }
        else
        {
            Debug.LogError("[InGameUIView] Leave button is null");
        }

        if (_returnToLobbyButton != null)
        {
            _returnToLobbyButton.clicked += () =>
            {
                OnReturnButtonClicked?.Invoke();
                _returnToLobbyButton.text = "Returning...";
                _returnToLobbyButton.SetEnabled(false);
            };
        }
        else
        {
            Debug.LogError("[InGameUIView] Return button is null");
        }
    }

    public void ShowEndGamePopup()
    {
        if (_endGamePopup != null)
        {
            _endGamePopup.ToggleInClassList("hidden");
        }

        if (_uiContainer != null)
        {
            _uiContainer.ToggleInClassList("hidden");
        }
    }
    
    public void Show()
    {
        if (_root != null) _root.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
    }
}

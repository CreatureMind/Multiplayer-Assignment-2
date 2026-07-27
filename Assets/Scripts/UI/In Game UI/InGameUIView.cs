using System;
using UnityEngine;
using UnityEngine.UIElements;

public class InGameUIView : MonoBehaviour
{
    public event Action OnEndGameButtonClicked;
    public event Action OnLeaveButtonClicked;
    public event Action OnReturnToLobbyClicked;

    private UIDocument _document;
    private VisualElement _root;

    private VisualElement _endGamePopup;
    private VisualElement _uiContainer;
    private Button _endGameButton;
    private Button _leaveGameButton;
    private Button _returnToLobbyButton;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();

        InitializeUI(_document);
    }

    private void InitializeUI(UIDocument document)
    {
        _root = document.rootVisualElement;

        _endGamePopup = _root.Q<VisualElement>(UI_In_Game_View.ended_popup);
        _uiContainer = _root.Q<VisualElement>(UI_In_Game_View.ui_container);
        _endGameButton = _root.Q<Button>(UI_In_Game_View.end_game_button);
        _leaveGameButton = _root.Q<Button>(UI_In_Game_View.leave_button);
        _returnToLobbyButton = _root.Q<Button>(UI_In_Game_View.return_button);

        SetupButtonCallbacks();
    }

    private void SetupButtonCallbacks()
    {
        if (_endGameButton != null)
        {
            _endGameButton.clicked += () => OnEndGameButtonClicked?.Invoke();
        }

        if (_leaveGameButton != null)
        {
            _leaveGameButton.clicked += () => OnLeaveButtonClicked?.Invoke();
        }

        if (_returnToLobbyButton != null)
        {
            _returnToLobbyButton.clicked += () => OnReturnToLobbyClicked?.Invoke();
        }
    }

    public void SetEndGameButtonVisible(bool visible)
    {
        if (_endGameButton != null)
        {
            _endGameButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void SetLeaveButtonVisible(bool visible)
    {
        if (_leaveGameButton != null)
        {
            _leaveGameButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

    public void HideEndGamePopup()
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
}

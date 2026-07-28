using System;
using UnityEngine;
using UnityEngine.UIElements;

public class InGameUIView : MonoBehaviour
{
    public event Action OnLeaveButtonClicked;
    public event Action OnReturnButtonClicked;

    private UIDocument _document;
    private VisualElement _root;

    private VisualElement _endGamePopup;
    private VisualElement _uiContainer;
    private Button _leaveGameButton;
    private Button _returnToLobbyButton;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        InitializeUI(_document);
    }

    private void InitializeUI(UIDocument document)
    {
        _root = document.rootVisualElement;

        _endGamePopup = _root.Q<VisualElement>(UI_In_Game_View.ended_popup);
        _uiContainer = _root.Q<VisualElement>(UI_In_Game_View.ui_container);
        _leaveGameButton = _root.Q<Button>(UI_In_Game_View.leave_button);
        _returnToLobbyButton = _root.Q<Button>(UI_In_Game_View.return_button);

        SetupButtonCallbacks();
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

        if (_returnToLobbyButton != null)
        {
            _returnToLobbyButton.clicked += () =>
            {
                OnReturnButtonClicked?.Invoke();
                _returnToLobbyButton.text = "Returning...";
                _returnToLobbyButton.SetEnabled(false);
            };
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
}

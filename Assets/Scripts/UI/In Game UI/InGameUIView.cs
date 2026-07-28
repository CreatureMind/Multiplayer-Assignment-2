using System;
using Unity.VisualScripting;
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
        if (!_document.GetComponent<PanelEventHandler>())
            _document.AddComponent<PanelEventHandler>();
        if (!_document.GetComponent<PanelRaycaster>())
            _document.AddComponent<PanelRaycaster>();
        
    }

    private void Start()
    {
        if (!_document)
        {
            Debug.LogError("UIDocument is null");
            return;
        }
        
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
                Debug.Log("<color=green>[UI VIEW] Leave Button Clicked!</color>");
                OnLeaveButtonClicked?.Invoke();
                _leaveGameButton.text = "Leaving...";
                _leaveGameButton.SetEnabled(false);
            };
        }
        else
        {
            Debug.LogError("Leave button is null");
        }

        if (_returnToLobbyButton != null)
        {
            _returnToLobbyButton.clicked += () =>
            {
                Debug.Log("<color=green>[UI VIEW] Return Button Clicked!</color>");
                OnReturnButtonClicked?.Invoke();
                _returnToLobbyButton.text = "Returning...";
                _returnToLobbyButton.SetEnabled(false);
            };
        }
        else
        {
            Debug.LogError("Return button is null");
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

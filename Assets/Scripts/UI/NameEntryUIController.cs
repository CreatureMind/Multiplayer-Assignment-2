using Events;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class NameEntryUIController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    
    private const string FIELD_PLAYER_NAME = "player-name-field";
    private const string BTN_CONFIRM = "confirm-button";
    private const string LABEL_ERROR = "error-label";
    
    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        ShowPanel();
    }
    
    private void ShowPanel()
    {
        _root = _doc.rootVisualElement;

        var nameField = _root.Q<TextField>(FIELD_PLAYER_NAME);
        var confirmBtn = _root.Q<Button>(BTN_CONFIRM);
        var errorLabel = _root.Q<Label>(LABEL_ERROR);

        if (nameField == null || confirmBtn == null)
        {
            Debug.LogError("[NameEntryUIController] Required UI elements not found.");
            return;
        }
        
        nameField.value = PlayerPrefs.GetString("PlayerName", string.Empty);
        if (errorLabel != null)
            errorLabel.style.display = DisplayStyle.None;

        confirmBtn.clicked += () => OnConfirmClicked(nameField, errorLabel);
    }
    
    private void OnConfirmClicked(TextField nameField, Label errorLabel)
    {
        var trimmed = nameField.value.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            if (errorLabel != null)
            {
                errorLabel.text = "Please enter a name.";
                errorLabel.style.display = DisplayStyle.Flex;
            }
            return;
        }

        if (trimmed.Length > 32)
        {
            if (errorLabel != null)
            {
                errorLabel.text = "Name must be 32 characters or fewer.";
                errorLabel.style.display = DisplayStyle.Flex;
            }
            return;
        }
        
        PlayerPrefs.SetString("PlayerName", trimmed);
        PlayerPrefs.Save();

        var localData = NetworkManager.Instance?.GetLocalPlayerData();
        if (localData)
            localData.DisplayName = trimmed;
        
        EventBus.Raise(new PlayerNameConfirmedEvent { PlayerName = trimmed });
        gameObject.SetActive(false);
    }
}
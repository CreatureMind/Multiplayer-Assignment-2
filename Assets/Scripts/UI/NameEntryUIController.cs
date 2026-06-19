using Events;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class NameEntryUIController : MonoBehaviour
{
    private enum NameEntryState { Hidden, EnteringName, Confirmed }
    private NameEntryState _state = NameEntryState.Hidden;
    
    private UIDocument _doc;
    private VisualElement _root;
    
    private string _confirmedName;

    private PlayerData _lastAppliedTo;
    
    private const string FIELD_PLAYER_NAME = "player-name-field";
    private const string BTN_CONFIRM = "confirm-button";
    private const string LABEL_ERROR = "error-label";
    
    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }
    
    private void OnEnable()
    {
        EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
    }
    
    private void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
    }

    private void Start()
    {
        ShowPanel();
    }
    
    private void OnPlayerListChanged(PlayerListChangedEvent e)
    {
        if (_state == NameEntryState.EnteringName)
        {
            var localData = NetworkManager.Instance?.GetLocalPlayerData();
            if (!localData)
                return;

            _root = _doc.rootVisualElement;
            var nameField = _root.Q<TextField>(FIELD_PLAYER_NAME);
            if (nameField == null)
                return;

            var currentName = localData.DisplayName.Value;
            if (!string.IsNullOrEmpty(currentName) && !currentName.StartsWith("Player_"))
                nameField.value = currentName;

            return;
        }

        // If the name was already confirmed but PlayerData wasn't ready at that point,
        // apply it now that PlayerData has spawned.
        if (_state == NameEntryState.Confirmed)
            TryApplyConfirmedName();
    }

    private void ShowPanel()
    {
        _state = NameEntryState.EnteringName;
        _root = _doc.rootVisualElement;

        var nameField = _root.Q<TextField>(FIELD_PLAYER_NAME);
        var confirmBtn = _root.Q<Button>(BTN_CONFIRM);
        var errorLabel = _root.Q<Label>(LABEL_ERROR);

        if (nameField == null || confirmBtn == null)
        {
            Debug.LogError("[NameEntryUIController] Required UI elements not found in Name_Entry_View.uxml.");
            return;
        }

        nameField.value = string.Empty;
        if (errorLabel != null)
            errorLabel.style.display = DisplayStyle.None;

        confirmBtn.clicked += () => OnConfirmClicked(nameField, errorLabel);
    }

    private void OnConfirmClicked(TextField nameField, Label errorLabel)
    {
        if (_state == NameEntryState.Confirmed)
            return;

        var trimmed = nameField.value.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            ShowError(errorLabel, "Please enter a name.");
            return;
        }

        if (trimmed.Length > 32)
        {
            ShowError(errorLabel, "Name must be 32 characters or fewer.");
            return;
        }

        _state = NameEntryState.Confirmed;
        _confirmedName = trimmed;
        _lastAppliedTo = null;

        TryApplyConfirmedName();

        gameObject.SetActive(false);
        EventBus.Raise(new PlayerNameConfirmedEvent { PlayerName = trimmed });
    }

    private void TryApplyConfirmedName()
    {
        if (_confirmedName == null)
            return;

        var localData = NetworkManager.Instance?.GetLocalPlayerData();
        if (!localData)
            return;

        if (localData == _lastAppliedTo)
            return;

        localData.ApplyConfirmedName(_confirmedName);
        _lastAppliedTo = localData;
    }

    private static void ShowError(Label errorLabel, string message)
    {
        if (errorLabel == null)
            return;
        errorLabel.text = message;
        errorLabel.style.display = DisplayStyle.Flex;
    }
}
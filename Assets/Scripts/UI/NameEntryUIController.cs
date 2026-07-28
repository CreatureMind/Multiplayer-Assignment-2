using System;
using System.Collections;
using Events;
using UnityEngine;
using UnityEngine.Networking;
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
    private const string BTN_RANDOM = "randomize-button";
    private const string LABEL_ERROR = "error-label";

    private const string RandomNameApi = "https://randomuser.me/api/?inc=login";
    
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
        // Returning from a match: name already confirmed, skip entry.
        if (NetworkManager.Instance && NetworkManager.Instance.IsReturningFromMatch)
        {
            _state = NameEntryState.Confirmed;
            _confirmedName = NetworkManager.Instance.LocalConfirmedName;
            gameObject.SetActive(false);
            return;
        }

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

        confirmBtn.clicked += () => OnConfirmClicked(nameField, errorLabel);

        // Optional: present only if the UXML has a "randomize-button".
        var randomBtn = _root.Q<Button>(BTN_RANDOM);
        if (randomBtn != null)
            randomBtn.clicked += () => StartCoroutine(FetchRandomName(nameField, errorLabel));
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

        if (NameAlreadyTaken(trimmed))
        {
            ShowError(errorLabel, $"The name \"{trimmed}\" is already taken. Please choose another.");
            return;
        }

        _state = NameEntryState.Confirmed;
        _confirmedName = trimmed;
        _lastAppliedTo = null;
        
        if (NetworkManager.Instance)
            NetworkManager.Instance.LocalConfirmedName = trimmed;

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

    // Pulls a name from a free API; falls back to a local generator on any failure
    // (offline, timeout, WebGL CORS, rate-limit) so the button never dead-ends.
    private IEnumerator FetchRandomName(TextField nameField, Label errorLabel)
    {
        using var req = UnityWebRequest.Get(RandomNameApi);
        yield return req.SendWebRequest();

        string name = null;
        if (req.result == UnityWebRequest.Result.Success)
            name = ParseUsername(req.downloadHandler.text);
        else
            Debug.LogWarning($"[NameEntryUIController] Random name request failed ({req.result}); using local fallback.");

        if (string.IsNullOrEmpty(name))
            name = LocalRandomName();

        if (name.Length > 32)
            name = name.Substring(0, 32);

        // Don't hand back a name we already know is taken.
        if (NameAlreadyTaken(name))
            name = LocalRandomName();

        nameField.value = name;
        ShowError(errorLabel, string.Empty);
    }

    private static string ParseUsername(string json)
    {
        try
        {
            var parsed = JsonUtility.FromJson<RandomUserResponse>(json);
            if (parsed?.results != null && parsed.results.Length > 0 && parsed.results[0].login != null)
                return parsed.results[0].login.username;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NameEntryUIController] Failed to parse random name: {e.Message}");
        }
        return null;
    }

    private static readonly string[] RandomAdjectives =
        { "Swift", "Brave", "Silent", "Crimson", "Lucky", "Shadow", "Iron", "Golden", "Wild", "Frost" };
    private static readonly string[] RandomNouns =
        { "Wolf", "Falcon", "Ranger", "Viper", "Knight", "Ghost", "Comet", "Tiger", "Raven", "Blade" };

    private static string LocalRandomName() =>
        $"{RandomAdjectives[UnityEngine.Random.Range(0, RandomAdjectives.Length)]}" +
        $"{RandomNouns[UnityEngine.Random.Range(0, RandomNouns.Length)]}" +
        $"{UnityEngine.Random.Range(1, 1000)}";

    [Serializable] private class RandomUserResponse { public RandomUser[] results; }
    [Serializable] private class RandomUser { public Login login; }
    [Serializable] private class Login { public string username; }

    // Matches against players currently known locally. Note: before joining the hub this
    // map is empty, so it only catches duplicates once connected; cross-player uniqueness
    // for the initial name still needs server-side validation on join.
    private static bool NameAlreadyTaken(string name)
    {
        if (!NetworkManager.Instance) return false;

        foreach (var p in NetworkManager.Instance.GetAllPlayers())
        {
            if (p == null || !p.Object || !p.Object.IsValid) continue;
            if (string.Equals(p.DisplayName.ToString(), name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void ShowError(Label errorLabel, string message)
    {
        if (errorLabel == null)
            return;
        errorLabel.text = message;
    }
}
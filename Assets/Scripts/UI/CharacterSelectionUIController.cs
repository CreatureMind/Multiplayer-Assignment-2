using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Events;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CharacterSelectionUIController : MonoBehaviour
{
    [SerializeField] private CharacterRegistry characterRegistry;

    private UIDocument _doc;
    private VisualElement _root;
    
    private readonly Dictionary<int, Button> _buttonMap = new();
    private readonly Dictionary<int, CharacterSlotState> _slotStates = new();
    
    private int _localPlayerCharacterId = -1;
    private Coroutine _waitForManagerRoutine;
    
    private const string CONTAINER_CHARACTER_GRID = "character-grid";
    
    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }
    
    private void OnEnable()
    {
        EventBus.Subscribe<CharacterSelectionManagerReadyEvent>(OnSelectionManagerReady);
        EventBus.Subscribe<CharacterClaimedEvent>(OnCharacterClaimed);
        EventBus.Subscribe<CharacterReleasedEvent>(OnCharacterReleased);
        EventBus.Subscribe<CharacterSelectionConfirmedEvent>(OnSelectionConfirmed);
        EventBus.Subscribe<CharacterSelectionDeniedEvent>(OnSelectionDenied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<CharacterSelectionManagerReadyEvent>(OnSelectionManagerReady);
        EventBus.Unsubscribe<CharacterClaimedEvent>(OnCharacterClaimed);
        EventBus.Unsubscribe<CharacterReleasedEvent>(OnCharacterReleased);
        EventBus.Unsubscribe<CharacterSelectionConfirmedEvent>(OnSelectionConfirmed);
        EventBus.Unsubscribe<CharacterSelectionDeniedEvent>(OnSelectionDenied);
        
        if (_waitForManagerRoutine != null)
        {
            StopCoroutine(_waitForManagerRoutine);
            _waitForManagerRoutine = null;
        }
    }
    
    private void Start()
    {
        gameObject.SetActive(true);
        
        if (CharacterSelectionManager.Instance)
            TryBuildGrid();
        else
            _waitForManagerRoutine = StartCoroutine(WaitForSelectionManagerThenBuild());
    }
    
    private IEnumerator WaitForSelectionManagerThenBuild()
    {
        const int maxFramesToWait = 300; // ~5 seconds at 60fps — generous safety margin.
        var framesWaited = 0;

        while (!CharacterSelectionManager.Instance && framesWaited < maxFramesToWait)
        {
            framesWaited++;
            yield return null;
        }

        _waitForManagerRoutine = null;

        if (!CharacterSelectionManager.Instance)
        {
            Debug.LogError("[CharacterSelectionUI] Timed out waiting for CharacterSelectionManager.Instance.");
            yield break;
        }

        TryBuildGrid();
    }
    
    private void OnSelectionManagerReady(CharacterSelectionManagerReadyEvent e) => TryBuildGrid();
    
    private void OnCharacterClaimed(CharacterClaimedEvent e)
    {
        var isLocal = NetworkManager.Instance &&
                      NetworkManager.Instance.IsLocalPlayer(e.ClaimedBy);

        _slotStates[e.CharacterId] = isLocal
            ? CharacterSlotState.TakenBySelf
            : CharacterSlotState.TakenByOther;

        RefreshButton(e.CharacterId);
    }
    
    private void OnCharacterReleased(CharacterReleasedEvent e)
    {
        _slotStates[e.CharacterId] = CharacterSlotState.Available;
        RefreshButton(e.CharacterId);
    }
    
    private void OnSelectionConfirmed(CharacterSelectionConfirmedEvent e)
    {
        _localPlayerCharacterId = e.CharacterId;
        gameObject.SetActive(false);
    }
    
    private void OnSelectionDenied(CharacterSelectionDeniedEvent e)
        => Debug.Log($"[CharacterSelectionUI] Character {e.CharacterId} was denied — already taken.");
    
    private void TryBuildGrid()
    {
        if (_buttonMap.Count > 0)
            return;
        if (!characterRegistry)
        {
            Debug.LogError("[CharacterSelectionUI] characterRegistry is NULL — assign it in the Inspector.");
            return;
        }
        if (!CharacterSelectionManager.Instance)
        {
            Debug.LogWarning("[CharacterSelectionUI] CharacterSelectionManager.Instance is null — grid not built yet.");
            return;
        }

        _root = _doc.rootVisualElement;
        var grid = _root.Q<VisualElement>(CONTAINER_CHARACTER_GRID);
        if (grid == null)
        {
            Debug.LogError("[CharacterSelectionUI] Could not find 'character-grid' container.");
            return;
        }

        grid.Clear();
        _buttonMap.Clear();
        _slotStates.Clear();

        foreach (var def in characterRegistry.Characters)
        {
            var button = BuildCharacterButton(def);
            grid.Add(button);
            _buttonMap[def.CharacterId] = button;
            
            _slotStates[def.CharacterId] = CharacterSelectionManager.Instance.IsCharacterClaimed(def.CharacterId)
                ? CharacterSlotState.TakenByOther
                : CharacterSlotState.Available;

            RefreshButton(def.CharacterId);
        }
    }
    
    private Button BuildCharacterButton(CharacterDefinition def)
    {
        var button = new Button
        {
            name = $"char-btn-{def.CharacterId}",
            text = def.CharacterName,
            style =
            {
                backgroundColor = new StyleColor(def.CharacterColor),
                width = new StyleLength(new Length(18, LengthUnit.Percent)),
                height = 120,
                marginLeft = 8,
                marginRight = 8,
                marginTop = 8,
                marginBottom = 8,
                fontSize = 22,
                color = new StyleColor(Color.white)
            }
        };

        var characterId = def.CharacterId;
        button.clicked += () => OnCharacterButtonClicked(characterId);

        return button;
    }
    
    private void OnCharacterButtonClicked(int characterId)
    {
        if (!CharacterSelectionManager.Instance)
        {
            Debug.LogWarning("[CharacterSelectionUI] CharacterSelectionManager not ready yet.");
            return;
        }
        
        var localPlayer = NetworkManager.Instance.GetLocalPlayerData();
        if (!localPlayer)
        {
            Debug.LogWarning("[CharacterSelectionUI] localPlayer is null — cannot send request.");
            return;
        }
        
        CharacterSelectionManager.Instance.RequestCharacterRpc(characterId, localPlayer.Object.InputAuthority);
    }
    
    private void RefreshButton(int characterId)
    {
        if (!_buttonMap.TryGetValue(characterId, out var button))
            return;

        var state = _slotStates.GetValueOrDefault(characterId, CharacterSlotState.Available);

        switch (state)
        {
            case CharacterSlotState.Available:
                button.SetEnabled(true);
                button.style.opacity = 1f;
                break;
            case CharacterSlotState.TakenByOther:
                button.SetEnabled(false);
                button.style.opacity = 0.35f;
                break;
            case CharacterSlotState.TakenBySelf:
                button.SetEnabled(true);
                button.style.opacity = 1f;
                button.style.borderTopWidth = 4;
                button.style.borderBottomWidth = 4;
                button.style.borderLeftWidth = 4;
                button.style.borderRightWidth = 4;
                button.style.borderTopColor = new StyleColor(Color.white);
                button.style.borderBottomColor = new StyleColor(Color.white);
                button.style.borderLeftColor = new StyleColor(Color.white);
                button.style.borderRightColor = new StyleColor(Color.white);
                break;
        }
    }
}
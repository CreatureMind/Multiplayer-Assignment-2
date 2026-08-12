using UnityEngine;
using UnityEngine.UIElements;

// Bridges the UI document with the scene context elements to execute input commands from the client
[RequireComponent(typeof(UIDocument))]
public class GameHudController : MonoBehaviour
{
    [SerializeField] private UIDocument document;

    private Button _endTurnButton;
    private Button _soldierButton;
    private Button _bombButton;
    private Button _baseButton;
    private BudgetHudView _budgetView;
    private Label _turnValueLabel;
    private bool _wired;
    
    private void Reset() => document = GetComponent<UIDocument>();

    // OnEnable catches re-enables (tree already built), Start catches the first spawn
    private void OnEnable() => TryWire();

    private void Start()
    {
        TryWire();
        ClientManager.OnPlayerTurnChanged += HandleTurnChange;
    }

    private void OnDisable()
    {
        if (!_wired)
            return;
        
        _budgetView?.Disable();
        _budgetView = null;
        
        if (_endTurnButton != null)
            _endTurnButton.clicked -= OnEndTurn;
        if (_soldierButton != null)
            _soldierButton.clicked -= OnSoldier;
        if (_bombButton != null)
            _bombButton.clicked -= OnBomb;
        if (_baseButton != null)
            _baseButton.clicked -= OnBase;
        _endTurnButton = _soldierButton = _bombButton = _baseButton = null;
        _wired = false;
    }

    private void TryWire()
    {
        if (_wired)
            return;
        
        if (!document)
            document = GetComponent<UIDocument>();

        var root = document ? document.rootVisualElement : null;
        if (root == null)
            return;
        
        _endTurnButton = root.Q<Button>("end-turn-button");
        _soldierButton = root.Q<Button>("pawn-button");
        _bombButton = root.Q<Button>("bomb-button");
        _baseButton = root.Q<Button>("base-button");
        
        Wire(_endTurnButton, "end-turn-button", OnEndTurn);
        Wire(_soldierButton, "pawn-button", OnSoldier);
        Wire(_bombButton, "bomb-button", OnBomb);
        Wire(_baseButton, "base-button", OnBase);
        
        var budgetValueLabel = root.Q<Label>("budget-value");
        if (budgetValueLabel == null)
            Debug.LogError("[GameHudController] Label 'budget-value' not found in the UXML.");
        _budgetView = new BudgetHudView(budgetValueLabel);
        _budgetView.Enable();
        
        _turnValueLabel = root.Q<Label>("turn-value");
        if (_turnValueLabel == null)
            Debug.LogError("[GameHudController] Label 'turn-value' not found in the UXML.");

        _wired = true;
    }

    private void HandleTurnChange(string value)
    {
        _turnValueLabel.text = value;
    }
    
    private static void Wire(Button button, string id, System.Action handler)
    {
        if (button == null)
        {
            Debug.LogError($"[GameHudController] Button '{id}' not found in the UXML.");
            return;
        }
        button.clicked += handler;
    }
    
    // Lazy resolve through client scene context
    private static InputHandler Input
    {
        get
        {
            var ctx = ClientSceneContext.Instance;
            if (ctx && ctx.InputHandler)
                return ctx.InputHandler;
            Debug.LogWarning("[GameHudController] No ClientSceneContext/InputHandler available yet.");
            return null;
        }
    }
    
    private void OnEndTurn() => Input?.SubmitPass();
    private void OnSoldier() => Input?.SelectMoveSoldier();
    private void OnBomb() => Input?.SelectPlaceBomb();
    private void OnBase()  => Input?.SelectBuildBase();
}
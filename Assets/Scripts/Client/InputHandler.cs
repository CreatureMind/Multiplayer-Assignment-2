using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Converts pointer input into a MoveRequest and raises it.
// It never validates and never mutates.
public sealed class InputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPosition;
    [SerializeField] private InputActionReference primaryAction;
    
    // A legal request the player just made.
    // ClientManager forwards this to the server RPC; this class deliberately knows nothing about networking.
    public event Action<MoveRequest> RequestSubmitted;
    
    // Hovered cell, or null when the pointer is off the board.
    public event Action<Vector2Int?> HoverChanged;

    private BoardCoordinateMapper _mapper;
    private PlayerActionController _actions;
    private Vector2Int? _lastHover;
    private bool _ready;
    
    private UIDocument _document;
    
    // Called once by ClientManager after the board dimensions are known
    public void Initialize(BoardCoordinateMapper mapper, PlayerActionController actions)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _ready = true;
        InGameUIView.OnObjectLoaded += SetDocument;
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += OnAnyActionChange;
        
        Debug.Log($"[Input] OnEnable ran, primaryAction={(primaryAction ? primaryAction.name : "NULL")}");
        
        if (pointerPosition)
            pointerPosition.action.Enable();
        if (primaryAction)
        {
            primaryAction.action.Enable();
            Debug.Log($"[Input] right after Enable: {primaryAction.action.enabled}");   // <-- add this
            primaryAction.action.performed += OnPrimaryPerformed;
        }
    }

    private static void OnAnyActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionDisabled)
            Debug.Log($"[Input] DISABLED: {obj}\n{Environment.StackTrace}");
    }

    private void SetDocument(UIDocument doc)
    {
        _document = doc;
    }

    private bool PointerOverToolkitUI(Vector2 screenPos)
    {
        var root = _document ? _document.rootVisualElement : null;
        if (root?.panel == null)
            return false;
        
        var panelPos = RuntimePanelUtils.ScreenToPanel(
            root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));

        return root.panel.Pick(panelPos) != null;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnAnyActionChange;
        
        if (primaryAction)
        {
            primaryAction.action.performed -= OnPrimaryPerformed;
            primaryAction.action.Disable();
        }
        if (pointerPosition)
            pointerPosition.action.Disable();
        
        InGameUIView.OnObjectLoaded -= SetDocument;
    }

    private void Update()
    {
        //if (primaryAction)
            // Debug.Log($"[Input] enabled={primaryAction.action.enabled} " +
            //           $"map={primaryAction.action.actionMap?.name} " +
            //           $"phase={primaryAction.action.phase}");
        
        if (!_ready || !pointerPosition)
            return;
        
        var screen = pointerPosition.action.ReadValue<Vector2>();
        var hover = _mapper.TryScreenToBoard(screen, out var cell)
            ? cell
            : (Vector2Int?)null;

        if (hover == _lastHover)
            return;
        _lastHover = hover;
        HoverChanged?.Invoke(hover);
    }

    private void OnPrimaryPerformed(InputAction.CallbackContext _)
    {
        Debug.Log("[Input] primary performed");
        if (!_ready || !pointerPosition)
        {
            Debug.Log("[Input] not ready");
            return;
        }
        
        var screen = pointerPosition.action.ReadValue<Vector2>();
        if (PointerOverToolkitUI(screen))
        {
            Debug.Log("[Input] over UI");
            return;
        }
        if (!_mapper.TryScreenToBoard(screen, out var cell))
            return;
        
        if (_actions.TryHandleClick(cell, out var request))
            RequestSubmitted?.Invoke(request);
        
        Debug.Log($"Cell: {cell.x},{cell.y}, Request:{request.ToString()}");
    }
    
    public void SubmitPass() => RequestSubmitted?.Invoke(MoveRequest.Pass);
    public void SelectMoveSoldier() => _actions?.SetMode(MoveIntent.MoveSoldier);
    public void SelectPlaceBomb() => _actions?.SetMode(MoveIntent.PlaceBomb);
    public void SelectBuildBase() => _actions?.SetMode(MoveIntent.BuildBase);
}

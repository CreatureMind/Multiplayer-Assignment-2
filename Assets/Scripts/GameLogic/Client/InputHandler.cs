using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Converts pointer input into a MoveRequest and raises it.
// Holds ZERO gameplay rules: it asks the controller whether a click means anything and forwards the result.
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
    
    // Called once by ClientManager after the board dimensions are known
    public void Initialize(BoardCoordinateMapper mapper, PlayerActionController actions)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _ready = true;
    }

    private void OnEnable()
    {
        if (pointerPosition)
            pointerPosition.action.Enable();
        if (primaryAction)
        {
            primaryAction.action.Enable();
            primaryAction.action.performed += OnPrimaryPerformed;
        }
    }

    private void OnDisable()
    {
        if (primaryAction)
        {
            primaryAction.action.performed -= OnPrimaryPerformed;
            primaryAction.action.Disable();
        }
        if (pointerPosition)
            pointerPosition.action.Disable();
    }

    private void Update()
    {
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
        if (!_ready || !pointerPosition)
            return;

        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;
        
        var screen = pointerPosition.action.ReadValue<Vector2>();
        if (!_mapper.TryScreenToBoard(screen, out var cell))
            return;
        
        if (_actions.TryHandleClick(cell, out var request))
            RequestSubmitted?.Invoke(request);
    }
    
    public void SubmitPass() => RequestSubmitted?.Invoke(MoveRequest.Pass);
}

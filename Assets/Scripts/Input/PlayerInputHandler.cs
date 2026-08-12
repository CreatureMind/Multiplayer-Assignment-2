using System;
//using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance;
    [SerializeField] private InputActionReference lmbAction;
    [SerializeField] private InputActionReference mousePosAction;
    
    public event Action<InputType, bool, Vector2> OnMouseInput;
    
    private Vector2 mouseScreenPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }
    
    public void OnEnable()
    {
        lmbAction.action.Enable();
        mousePosAction.action.Enable();
        
        lmbAction.action.performed += ctx => InvokeOnInput(InputType.LMB, true);
        lmbAction.action.canceled += ctx => InvokeOnInput(InputType.LMB, false);
        
        mousePosAction.action.performed += ctx =>
        {
            mouseScreenPosition = ctx.ReadValue<Vector2>();
        };
    }
    
    private void InvokeOnInput(InputType inputType, bool isPressed)
    {
        OnMouseInput?.Invoke(inputType, isPressed, mouseScreenPosition);
    }

    public void OnDisable()
    {
        lmbAction.action.Disable();
        
        lmbAction.action.performed -= ctx => InvokeOnInput(InputType.LMB, true);
        lmbAction.action.canceled -= ctx => InvokeOnInput(InputType.LMB, false);
    }
}

public enum InputType
{
    LMB
}

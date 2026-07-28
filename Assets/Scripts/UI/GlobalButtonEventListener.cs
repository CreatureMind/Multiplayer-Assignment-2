using Events;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GlobalButtonEventListener : MonoBehaviour
{
    [SerializeField] private bool debug = false;
    
    private UIDocument uiDocument;
    private VisualElement root;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        if (root != null)
        {
            root.RegisterCallback<PointerEnterEvent>(OnGlobalPointerEnter, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(OnGlobalPointerLeave, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnGlobalClick, TrickleDown.TrickleDown);
        }
    }

    private void OnDisable()
    {
        if (root != null)
        {
            root.UnregisterCallback<PointerEnterEvent>(OnGlobalPointerEnter);
            root.UnregisterCallback<PointerLeaveEvent>(OnGlobalPointerLeave);
            root.UnregisterCallback<ClickEvent>(OnGlobalClick);
        }
    }

    #region Hover Events
    private void OnGlobalPointerEnter(PointerEnterEvent evt)
    {
        if (evt.target is Button button)
        {
            OnButtonHoverEnter(button);
        }
    }

    private void OnGlobalPointerLeave(PointerLeaveEvent evt)
    {
        if (evt.target is Button button)
        {
            OnButtonHoverExit(button);
        }
    }
    #endregion

    #region Click / Submit Events
    private void OnGlobalClick(ClickEvent evt)
    {
        if (evt.target is Button button)
        {
            OnButtonClick(button);
        }
    }

    private void OnGlobalSubmit(NavigationSubmitEvent evt)
    {
        if (evt.target is Button button)
        {
            OnButtonClick(button);
        }
    }
    #endregion

    #region Custom Logic Handlers
    private void OnButtonHoverEnter(Button button)
    {
        if (debug)
            Debug.Log($"[Hover Enter] {button.name}");
        EventBus.Raise(new PlaySoundEvent(){ SoundName = SoundEffectEnum.BTN_HOVER});
    }

    private void OnButtonHoverExit(Button button)
    {
        if (debug)
            Debug.Log($"[Hover Exit] {button.name}");
    }

    private void OnButtonClick(Button button)
    {
        if (debug)
            Debug.Log($"[Click / Submit] Global event invoked for: {button.name}");
        EventBus.Raise(new PlaySoundEvent(){ SoundName = SoundEffectEnum.BTN_CLICK});
    }
    #endregion
}

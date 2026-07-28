using Events;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DynamicGlobalButtonListener : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement currentRoot;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        
        // Listen to the UIDocument's container layout change
        if (uiDocument != null)
        {
            currentRoot = uiDocument.rootVisualElement;
            SubscribeToRoot(currentRoot);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromRoot(currentRoot);
    }

    private void Update()
    {
        // Fail-safe: Detect if the UI Manager swapped the underlying root asset
        if (uiDocument != null && uiDocument.rootVisualElement != currentRoot)
        {
            ResetSubscriptions();
        }
    }

    private void ResetSubscriptions()
    {
        UnsubscribeFromRoot(currentRoot);
        currentRoot = uiDocument.rootVisualElement;
        SubscribeToRoot(currentRoot);
    }

    private void SubscribeToRoot(VisualElement root)
    {
        if (root == null) return;

        // 1. Listen for structural layout resets (when a new UXML layout finishes rendering)
        root.RegisterCallback<GeometryChangedEvent>(OnHierarchyRebuilt);

        // 2. Bind your universal click and hover listeners
        root.RegisterCallback<PointerEnterEvent>(OnGlobalPointerEnter, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerLeaveEvent>(OnGlobalPointerLeave, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationSubmitEvent>(OnGlobalSubmit, TrickleDown.TrickleDown);
        root.RegisterCallback<ClickEvent>(OnGlobalClick, TrickleDown.TrickleDown);
    }

    private void UnsubscribeFromRoot(VisualElement root)
    {
        if (root == null) return;

        root.UnregisterCallback<GeometryChangedEvent>(OnHierarchyRebuilt);
        root.UnregisterCallback<PointerEnterEvent>(OnGlobalPointerEnter);
        root.UnregisterCallback<PointerLeaveEvent>(OnGlobalPointerLeave);
        root.UnregisterCallback<NavigationSubmitEvent>(OnGlobalSubmit);
        root.UnregisterCallback<ClickEvent>(OnGlobalClick);
    }

    private void OnHierarchyRebuilt(GeometryChangedEvent evt)
    {
        // When your manager instantiates a new UXML, the layout geometry calculation shifts.
        // If the root object reference itself shifted, we realign.
        if (uiDocument.rootVisualElement != currentRoot)
        {
            ResetSubscriptions();
        }
    }

    #region Input Event Interception
    private void OnGlobalPointerEnter(PointerEnterEvent evt)
    {
        if (evt.target is Button button) OnButtonHoverEnter(button);
    }

    private void OnGlobalPointerLeave(PointerLeaveEvent evt)
    {
        if (evt.target is Button button) OnButtonHoverExit(button);
    }

    private void OnGlobalClick(ClickEvent evt)
    {
        if (evt.target is Button button) OnButtonClick(button);
    }

    private void OnGlobalSubmit(NavigationSubmitEvent evt)
    {
        if (evt.target is Button button) OnButtonClick(button);
    }
    #endregion

    #region Custom Logic Handlers
    private void OnButtonHoverEnter(Button button)
    {
        EventBus.Raise(new PlaySoundEvent(){ SoundName = SoundEffectEnum.BTN_HOVER});
        Debug.Log($"[Hover Enter] {button.name}");
    }

    private void OnButtonHoverExit(Button button) => Debug.Log($"[Hover Exit] {button.name}");
    private void OnButtonClick(Button button)
    {
        EventBus.Raise(new PlaySoundEvent(){ SoundName = SoundEffectEnum.BTN_CLICK});
        Debug.Log($"[Click / Submit] {button.name}");
    }

    #endregion
}

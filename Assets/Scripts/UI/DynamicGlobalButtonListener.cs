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
        if (uiDocument)
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
        if (uiDocument && uiDocument.rootVisualElement != currentRoot)
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

        //Listen for structural layout resets (when a new UXML layout finishes rendering)
        root.RegisterCallback<GeometryChangedEvent>(OnHierarchyRebuilt);

        //Bind universal click and hover listeners
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
    }

    private void OnButtonHoverExit(Button button) { }

    private void OnButtonClick(Button button)
    {
        EventBus.Raise(new PlaySoundEvent(){ SoundName = SoundEffectEnum.BTN_CLICK});
    }

    #endregion
}

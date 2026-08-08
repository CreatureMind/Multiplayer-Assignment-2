using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Common
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class BaseOverlayView : MonoBehaviour
    {
        protected UIDocument Document { get; private set; }
        protected VisualElement Root { get; private set; }
        protected VisualElement Tint { get; private set; }
        protected VisualElement Container { get; private set; }

        // Element names from UXML
        protected virtual string TintName => "tint";
        protected virtual string ContainerName => "container";

        // USS modifier classes
        protected virtual string TintHiddenClass => "overlay-tint--hidden";
        protected virtual string ContainerHiddenClass => "overlay-container--hidden";

        public bool IsVisible { get; private set; } = true;

        protected virtual void Awake()
        {
            Document = GetComponent<UIDocument>();
        }

        protected virtual void Start()
        {
            if (!Document)
            {
                Debug.LogError($"[{GetType().Name}] UIDocument is null!");
                return;
            }

            InitializeOverlay();
        }

        private void InitializeOverlay()
        {
            Root = Document.rootVisualElement;

            if (Root != null)
            {
                Tint = Root.Q<VisualElement>(TintName);
                Tint?.AddToClassList(TintHiddenClass);
                
                Container = Root.Q<VisualElement>(ContainerName);
                Container?.AddToClassList(ContainerHiddenClass);
            }
            
            OnInitializeUI();
        }
        
        protected abstract void OnInitializeUI();

        public virtual void Show()
        {
            if (IsVisible) return;
            IsVisible = true;

            if (Document) Document.sortingOrder = UIOverlaySorter.PushOverlay();

            if (Root != null)
            {
                SetPickingModeRecursive(Tint, PickingMode.Position);
                Root.style.display = DisplayStyle.Flex;

                Root.schedule.Execute(() =>
                {
                    Tint?.RemoveFromClassList(TintHiddenClass);
                    Container?.RemoveFromClassList(ContainerHiddenClass);
                }).StartingIn(16);
            }

            OnShow();
        }

        public virtual void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;

            if (Root != null)
            {
                SetPickingModeRecursive(Tint, PickingMode.Ignore);

                Tint?.AddToClassList(TintHiddenClass);
                Container?.AddToClassList(ContainerHiddenClass);

                if (Container != null)
                {
                    Container.RegisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
                }
                else
                {
                    Root.style.display = DisplayStyle.None;
                    UIOverlaySorter.PopOverlay();
                }
            }

            OnHide();
        }

        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            Container?.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);

            if (!IsVisible && Root != null)
            {
                Root.style.display = DisplayStyle.None;
                UIOverlaySorter.PopOverlay();
            }
        }

        protected void SetPickingModeRecursive(VisualElement element, PickingMode mode)
        {
            if (element == null) return;
            element.pickingMode = mode;
            element.Query<VisualElement>().ForEach(child => child.pickingMode = mode);
        }
        
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}
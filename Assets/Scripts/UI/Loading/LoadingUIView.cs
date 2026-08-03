using Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Loading
{
    [RequireComponent(typeof(UIDocument))]
    public class LoadingUIView : MonoBehaviour
    {
        private UIDocument    _document;
        private VisualElement _root;
        private VisualElement _loadingSpinner;
        
        private bool _isVisible;
        private bool _canSpin;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[LoadingUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;
            _loadingSpinner = _root.Q<VisualElement>(UI_Loading_View.loading_spinner);
            
            Hide();
        }
        
        private void StartSpinning()
        {
            _canSpin = true;
            float currentAngle = 0f;

            _loadingSpinner.schedule.Execute(() =>
            {
                currentAngle += 1.5f; // degrees per tick (approx 360°/sec at ~60 FPS)
                if (currentAngle >= 360f) currentAngle -= 360f;

                _loadingSpinner.style.rotate = new Rotate(new Angle(currentAngle, AngleUnit.Degree));
            }).Every(16).Until(() => !_canSpin);
        }

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;
            
            if (_document) _document.sortingOrder = UIOverlaySorter.PushOverlay();
            
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            
            if (_loadingSpinner != null)
            {
                StartSpinning();
            }
            else
            {
                Debug.LogError("[LoadingUIView] Could not find VisualElement named 'loading-spinner' in Loading_View.");
            }
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;
            
            _canSpin = false;
            
            if (_root != null) _root.style.display = DisplayStyle.None;
            
            UIOverlaySorter.PopOverlay();
        }
    }
}
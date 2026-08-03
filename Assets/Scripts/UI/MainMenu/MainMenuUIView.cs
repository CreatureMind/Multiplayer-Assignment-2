using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.MainMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIView : MonoBehaviour
    {
        public event Action OnPlayClicked;
        public event Action OnOptionsClicked;
        public event Action OnCreditsClicked;

        private UIDocument    _document;
        private VisualElement _root;

        private Button _playGameBtn;
        private Button _optionsBtn;
        private Button _creditsBtn;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[MainMenuUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _playGameBtn = _root.Q<Button>(UI_Main_Menu_View.play_game_btn);
            _optionsBtn = _root.Q<Button>(UI_Main_Menu_View.options_btn);
            _creditsBtn = _root.Q<Button>(UI_Main_Menu_View.credits_btn);

            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_playGameBtn != null)
            {
                _playGameBtn.clicked += () => OnPlayClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[MainMenuUIView] Could not find Button named 'play-game-btn' in Main_Menu_View.");
            }

            if (_optionsBtn != null)
            {
                _optionsBtn.clicked += () => OnOptionsClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[MainMenuUIView] Could not find Button named 'options-btn' in Main_Menu_View.");
            }

            if (_creditsBtn != null)
            {
                _creditsBtn.clicked += () => OnCreditsClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[MainMenuUIView] Could not find Button named 'credits-btn' in Main_Menu_View.");
            }
        }
        
        public void Show()
        {
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }
    }
}
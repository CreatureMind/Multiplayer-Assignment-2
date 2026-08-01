using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Options
{
    [RequireComponent(typeof(UIDocument))]
    public class OptionsUIView : MonoBehaviour
    {
        public event Action<int>  OnMusicVolumeChanged;
        public event Action<int>  OnSfxVolumeChanged;
        public event Action<bool> OnMusicMuteToggled;
        public event Action<bool> OnSfxMuteToggled;
        public event Action       OnBackButtonClicked;

        private UIDocument    _document;
        private VisualElement _root;

        private SliderInt _musicVolumeSlider;
        private SliderInt _soundFXSlider;
        private Toggle    _musicMuteToggle;
        private Toggle    _soundFXMuteToggle;
        private Button    _backButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (!_document)
            {
                Debug.LogError("[OptionsUIView] UIDocument is null!");
                return;
            }

            InitializeUI(_document);
        }

        private void InitializeUI(UIDocument document)
        {
            _root = document.rootVisualElement;

            _musicVolumeSlider = _root.Q<SliderInt>(UI_Options_View.music_volume_slider);
            _soundFXSlider     = _root.Q<SliderInt>(UI_Options_View.sfx_volume_slider);
            _musicMuteToggle   = _root.Q<Toggle>   (UI_Options_View.mute_music_toggle);
            _soundFXMuteToggle = _root.Q<Toggle>   (UI_Options_View.mute_sfx_toggle);
            _backButton        = _root.Q<Button>   (UI_Options_View.back_button);

            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.RegisterValueChangedCallback(evt => OnMusicVolumeChanged?.Invoke(evt.newValue));
            }
            else
            {
                Debug.LogError("[OptionsUIView] Could not find SliderInt named 'music-volume-slider' in Options_View.");
            }

            if (_soundFXSlider != null)
            {
                _soundFXSlider.RegisterValueChangedCallback(evt => OnSfxVolumeChanged?.Invoke(evt.newValue));
            }
            else
            {
                Debug.LogError("[OptionsUIView] Could not find SliderInt named 'sfx-volume-slider' in Options_View.");
            }

            if (_musicMuteToggle != null)
            {
                _musicMuteToggle.RegisterValueChangedCallback(evt => OnMusicMuteToggled?.Invoke(evt.newValue));
            }
            else
            {
                Debug.LogError("[OptionsUIView] Could not find Toggle named 'mute-music-toggle' in Options_View.");
            }

            if (_soundFXMuteToggle != null)
            {
                _soundFXMuteToggle.RegisterValueChangedCallback(evt => OnSfxMuteToggled?.Invoke(evt.newValue));
            }
            else
            {
                Debug.LogError("[OptionsUIView] Could not find Toggle named 'mute-sfx-toggle' in Options_View.");
            }

            if (_backButton != null)
            {
                _backButton.clicked += () => OnBackButtonClicked?.Invoke();
            }
            else
            {
                Debug.LogError("[OptionsUIView] Could not find Button named 'back-button' in Options_View.");
            }
            
            Hide();
        }

        // View Setters (Using SetValueWithoutNotify to prevent event loops)
        public void SetMusicVolume(int value)  => _musicVolumeSlider?.SetValueWithoutNotify(value);
        public void SetSfxVolume(int value)    => _soundFXSlider?.SetValueWithoutNotify(value);
        public void SetMusicMute(bool isMuted) => _musicMuteToggle?.SetValueWithoutNotify(isMuted);
        public void SetSfxMute(bool isMuted)   => _soundFXMuteToggle?.SetValueWithoutNotify(isMuted);
        
        public void Show()
        {
            if (_document) _document.sortingOrder = UIOverlaySorter.PushOverlay();
            
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            
            UIOverlaySorter.PopOverlay();
        }
    }
}
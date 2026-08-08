using System;
using UnityEngine;

namespace UI.Options
{
    public class OptionsUIPresenter
    {
        private readonly OptionsUIModel _model;
        private readonly OptionsUIView  _view;

        private int _currentMusicSliderValue;
        private int _currentSfxSliderValue;

        public OptionsUIPresenter(OptionsUIModel model, OptionsUIView view)
        {
            _model = model;
            _view  = view;

            SubscribeToEvents();
            InitializeView();
        }

        private void SubscribeToEvents()
        {
            _view.OnMusicVolumeChanged += HandleMusicVolumeChanged;
            _view.OnSfxVolumeChanged   += HandleSfxVolumeChanged;
            _view.OnMusicMuteToggled   += HandleMusicMuteToggled;
            _view.OnSfxMuteToggled     += HandleSfxMuteToggled;
            _view.OnBackButtonClicked  += HandleBackButtonClicked;
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnMusicVolumeChanged -= HandleMusicVolumeChanged;
            _view.OnSfxVolumeChanged   -= HandleSfxVolumeChanged;
            _view.OnMusicMuteToggled   -= HandleMusicMuteToggled;
            _view.OnSfxMuteToggled     -= HandleSfxMuteToggled;
            _view.OnBackButtonClicked  -= HandleBackButtonClicked;
        }

        private void InitializeView()
        {
            _currentMusicSliderValue = _model.GetMusicVolumeSliderValue();
            _currentSfxSliderValue   = _model.GetSfxVolumeSliderValue();

            _view.SetMusicVolume(_currentMusicSliderValue);
            _view.SetSfxVolume  (_currentSfxSliderValue);

            _view.SetMusicMute(_model.IsMusicMuted());
            _view.SetSfxMute  (_model.IsSfxMuted());
        }

        private void HandleMusicVolumeChanged(int newValue)
        {
            _currentMusicSliderValue = newValue;
            _model.SetMusicVolume(newValue);
        }

        private void HandleSfxVolumeChanged(int newValue)
        {
            _currentSfxSliderValue = newValue;
            _model.SetSfxVolume(newValue);
        }

        private void HandleMusicMuteToggled(bool isMuted)
        {
            _model.SetMusicMute(isMuted, _currentMusicSliderValue);
        }

        private void HandleSfxMuteToggled(bool isMuted)
        {
            _model.SetSfxMute(isMuted, _currentSfxSliderValue);
        }

        private void HandleBackButtonClicked()
        {
            _view.Hide();
        }
    }
}
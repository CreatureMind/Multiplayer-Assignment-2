using UnityEngine;
using UnityEngine.Audio;

namespace UI.Options
{
    public class OptionsUIModel
    {
        private const string MUSIC_PARAM   = "MusicVolume";
        private const string SFX_PARAM     = "SfxVolume";
        private const float MUTE_DB        = -80f;
        private const float MUTE_THRESHOLD = -79f;

        private readonly AudioMixer _audioMixer;

        public OptionsUIModel(AudioMixer audioMixer)
        {
            _audioMixer = audioMixer;
        }

        public int GetMusicVolumeSliderValue() => GetVolumeSliderValue(MUSIC_PARAM);
        public int GetSfxVolumeSliderValue()   => GetVolumeSliderValue(SFX_PARAM);

        public bool IsMusicMuted() => IsMuted(MUSIC_PARAM);
        public bool IsSfxMuted()   => IsMuted(SFX_PARAM);

        public void SetMusicVolume(int sliderValue) => SetVolumeFromSlider(MUSIC_PARAM, sliderValue);
        public void SetSfxVolume(int sliderValue)   => SetVolumeFromSlider(SFX_PARAM, sliderValue);

        public void SetMusicMute(bool isMuted, int currentSliderValue) => SetMuteState(MUSIC_PARAM, isMuted, currentSliderValue);
        public void SetSfxMute(bool isMuted, int currentSliderValue)   => SetMuteState(SFX_PARAM, isMuted, currentSliderValue);

        // Helper calculations
        private int GetVolumeSliderValue(string parameterName)
        {
            if (_audioMixer && _audioMixer.GetFloat(parameterName, out var db))
            {
                var linearPct = Mathf.Pow(10f, db / 20f);
                return Mathf.RoundToInt(linearPct * 10f);
            }
            return 10;
        }

        private bool IsMuted(string parameterName)
        {
            if (_audioMixer && _audioMixer.GetFloat(parameterName, out var db))
            {
                return db <= MUTE_THRESHOLD;
            }
            return false;
        }

        private void SetVolumeFromSlider(string parameterName, int sliderValue)
        {
            if (!_audioMixer) return;
            var normalizedValue = Mathf.Max(sliderValue / 10f, 0.0001f);
            var db = Mathf.Log10(normalizedValue) * 20f;
            _audioMixer.SetFloat(parameterName, db);
        }

        private void SetMuteState(string parameterName, bool isMuted, int currentSliderValue)
        {
            if (!_audioMixer) return;

            if (isMuted)
            {
                _audioMixer.SetFloat(parameterName, MUTE_DB);
            }
            else
            {
                // Restore slider value when unmuted
                SetVolumeFromSlider(parameterName, currentSliderValue);
            }
        }
    }
}
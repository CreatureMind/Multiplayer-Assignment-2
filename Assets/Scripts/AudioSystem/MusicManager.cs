using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem {
    public class MusicManager : PersistentSingleton<MusicManager> {
        private float                     fading;
        private float                     previousStartVolume;
        private AudioSource               current;
        private AudioSource               previous;
        private AudioSource               sourceA;
        private AudioSource               sourceB;
        private readonly Queue<AudioClip> playlist = new();

        [SerializeField, Range(0.1f, 1)] private float volume = 1f;
        [SerializeField, Range(0,10)] private float    crossFadeTime = 1.0f;
        [SerializeField] private List<AudioClip>       initialPlaylist;
        [SerializeField] private AudioMixerGroup       musicMixerGroup;

        protected override void Awake() {
            base.Awake();
            EnsureSources();
        }

        void Start() {
            foreach (var clip in initialPlaylist) {
                AddToPlaylist(clip);
            }
        }

        public void AddToPlaylist(AudioClip clip) {
            playlist.Enqueue(clip);
            if (current == null && previous == null) {
                PlayNextTrack();
            }
        }

        public void Clear() => playlist.Clear();

        public void PlayNextTrack() {
            if (playlist.TryDequeue(out AudioClip nextTrack)) {
                Play(nextTrack);
            }
        }

        public void Play(AudioClip clip) {
            EnsureSources();
            if (current && current.clip == clip) return;

            if (previous) {
                // If we're asked to start a new track mid-fade, drop the older faded-out source.
                previous.Stop();
                previous.clip = null;
                previous = null;
            }

            previous = current;
            if (previous) previousStartVolume = previous.volume;

            // Swap between two dedicated sources so "previous" and "current" are never the same AudioSource.
            current = (current == sourceA) ? sourceB : sourceA;
            current.clip = clip;
            current.outputAudioMixerGroup = musicMixerGroup; // Set mixer group
            current.loop = true; // Theme loops until explicitly switched (eg day/night).
            current.volume = 0;
            current.bypassListenerEffects = true;
            current.playOnAwake = false;
            current.Play();

            fading = 0.001f;
        }

        void Update() {
            HandleCrossFade();

            if (current && !current.isPlaying && playlist.Count > 0) {
                PlayNextTrack();
            }
        }

        void HandleCrossFade() {
            if (fading <= 0f) return;
            
            fading += Time.deltaTime;

            float fraction = Mathf.Clamp01(fading / crossFadeTime);

            // Logarithmic fade
            float logFraction = fraction.ToLogarithmicFraction();

            if (previous) previous.volume = Mathf.Lerp(previousStartVolume, 0.0f, logFraction);
            if (current) current.volume = volume * logFraction;

            if (fraction >= 1) {
                fading = 0.0f;
                if (current) current.volume = volume;
                if (previous) {
                    previous.Stop();
                    previous.clip = null;
                    previous.volume = 0;
                    previous = null;
                }
            }
        }

        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (fading <= 0.0f && current) current.volume = volume;
        }

        void EnsureSources() {
            if (sourceA && sourceB) return;

            sourceA = GetOrCreateChildSource("MusicSourceA");
            sourceB = GetOrCreateChildSource("MusicSourceB");

            // Initialize current to a stable known source so the first swap picks the other one.
            if (current == null) current = sourceA;
        }

        AudioSource GetOrCreateChildSource(string childName) {
            var child = transform.Find(childName);
            if (!child) {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            var src = child.GetComponent<AudioSource>();
            if (!src) src = child.gameObject.AddComponent<AudioSource>();
            return src;
        }
    }
}

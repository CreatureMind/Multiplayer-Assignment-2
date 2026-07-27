using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AudioSystem {
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour {
        public SoundData Data { get; private set; }
        public LinkedListNode<SoundEmitter> Node { get; set; }

        /// <summary>Fired when the sound finishes naturally or is stopped. Cleared on every stop.</summary>
        public event Action OnComplete;

        AudioSource audioSource;
        Coroutine playingCoroutine;
        bool isActive;

        // When set, the emitter tracks this transform's position each frame so the sound follows a
        // moving source. We follow-by-position (not reparenting) so a pooled emitter can't be
        // destroyed along with the source.
        Transform followTarget;

        // Pitch is applied in Play() (not eagerly) so a looping clip can re-roll it each cycle.
        float basePitch;
        bool randomizePitch;
        float pitchMin = -0.05f;
        float pitchMax = 0.05f;

        void Awake() {
            audioSource = gameObject.GetOrAdd<AudioSource>();
        }

        void LateUpdate() {
            if (!isActive) return;
            if (followTarget == null) return;
            transform.position = followTarget.position;
        }

        public void Initialize(SoundData data) {
            Data = data;
            followTarget = null; // reset per acquisition from the pool
            audioSource.clip = data.clip;
            audioSource.outputAudioMixerGroup = data.mixerGroup;
            audioSource.loop = data.loop;
            audioSource.playOnAwake = data.playOnAwake;
            
            audioSource.mute = data.mute;
            audioSource.bypassEffects = data.bypassEffects;
            audioSource.bypassListenerEffects = data.bypassListenerEffects;
            audioSource.bypassReverbZones = data.bypassReverbZones;
            
            audioSource.priority = data.priority;
            audioSource.volume = data.volume;
            audioSource.pitch = data.pitch;
            basePitch = data.pitch;
            randomizePitch = false; // reset per acquisition from the pool
            audioSource.panStereo = data.panStereo;
            audioSource.spatialBlend = data.spatialBlend;
            audioSource.reverbZoneMix = data.reverbZoneMix;
            audioSource.dopplerLevel = data.dopplerLevel;
            audioSource.spread = data.spread;
            
            audioSource.minDistance = data.minDistance;
            audioSource.maxDistance = data.maxDistance;
            
            audioSource.ignoreListenerVolume = data.ignoreListenerVolume;
            audioSource.ignoreListenerPause = data.ignoreListenerPause;
            
            audioSource.rolloffMode = data.rolloffMode;
        }

        public void SetFollowTarget(Transform target) {
            followTarget = target;
            if (isActive && followTarget != null) {
                transform.position = followTarget.position;
            }
        }

        public void Play() {
            if (playingCoroutine != null) {
                StopCoroutine(playingCoroutine);
            }

            isActive = true;

            if (Data.loop && randomizePitch) {
                // Drive the loop ourselves so the pitch re-rolls on every repeat instead of
                // being locked to whatever it rolled on the first play.
                audioSource.loop = false;
                playingCoroutine = StartCoroutine(LoopWithRandomPitch());
            } else {
                audioSource.loop = Data.loop;
                ApplyPitch();
                audioSource.Play();
                playingCoroutine = StartCoroutine(WaitForSoundToEnd());
            }
        }

        IEnumerator WaitForSoundToEnd() {
            yield return new WaitWhile(() => audioSource.isPlaying);
            Stop();
        }

        IEnumerator LoopWithRandomPitch() {
            while (true) {
                ApplyPitch();
                audioSource.Play();
                yield return new WaitWhile(() => audioSource.isPlaying);
            }
        }

        void ApplyPitch() {
            audioSource.pitch = randomizePitch ? basePitch + Random.Range(pitchMin, pitchMax) : basePitch;
        }

        public void Stop() {
            // Guard against double-stop: once stopped the emitter is back in the pool and may
            // already be playing a different sound. Ignore stale Stop() calls.
            if (!isActive) return;
            isActive = false;

            if (playingCoroutine != null) {
                StopCoroutine(playingCoroutine);
                playingCoroutine = null;
            }

            audioSource.Stop();
            followTarget = null;

            Action callback = OnComplete;
            OnComplete = null;

            SoundManager.Instance.ReturnToPool(this);
            callback?.Invoke();
        }

        public void WithRandomPitch(float min = -0.05f, float max = 0.05f) {
            randomizePitch = true;
            pitchMin = min;
            pitchMax = max;
        }
    }
}

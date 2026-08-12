using UnityEngine;

namespace AudioSystem {
    public class SoundBuilder {
        readonly SoundManager soundManager;
        Vector3 position = Vector3.zero;
        Transform followTarget;
        bool randomPitch;
        float pitchRange = 0.05f;

        public SoundBuilder(SoundManager soundManager) {
            this.soundManager = soundManager;
        }

        public SoundBuilder WithPosition(Vector3 position) {
            this.position = position;
            return this;
        }

        public SoundBuilder WithFollowTarget(Transform target) {
            followTarget = target;
            return this;
        }

        public SoundBuilder WithRandomPitch(float range = 0.05f) {
            this.randomPitch = true;
            this.pitchRange = range;
            return this;
        }

        public SoundEmitter Play(SoundData soundData) {
            if (soundData == null) {
                Debug.LogError("SoundData is null");
                return null;
            }

            if (!soundManager.CanPlaySound(soundData)) return null;

            SoundEmitter soundEmitter = soundManager.Get();
            soundEmitter.Initialize(soundData);
            soundEmitter.transform.parent = soundManager.transform;

            if (followTarget != null) {
                soundEmitter.SetFollowTarget(followTarget);
                soundEmitter.transform.position = followTarget.position;
            } else {
                soundEmitter.transform.position = position;
            }

            if (randomPitch) {
                soundEmitter.WithRandomPitch(-pitchRange, pitchRange);
            }

            if (soundData.frequentSound) {
                soundEmitter.Node = soundManager.FrequentSoundEmitters.AddLast(soundEmitter);
            }

            soundEmitter.Play();
            return soundEmitter;
        }
    }
}

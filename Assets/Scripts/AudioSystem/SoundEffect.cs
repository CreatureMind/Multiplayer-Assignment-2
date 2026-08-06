using System;
using System.Collections;
using UnityEngine;

namespace AudioSystem {
    /// <summary>
    /// A single, inspector-configurable playable sound. Wrap one of these in any script
    /// (or in <see cref="AudioEmitter"/>) and call <see cref="Play(Vector3)"/> to fire it
    /// through the pooled <see cref="SoundManager"/>.
    ///
    /// The wrapper owns the instance it starts, so you can simply call <see cref="Stop"/>
    /// (handy for looping clips) without ever touching the pooled emitter directly. Play
    /// calls are fluent, so follow-up sounds can be chained with <see cref="PlayOnEnd"/> /
    /// <see cref="PlayDelayed"/>.
    /// </summary>
    [Serializable]
    public class SoundEffect {
        [SerializeField] SoundData data;
        [SerializeField] bool randomPitch;
        [Tooltip("Max pitch offset (+/-) applied when Random Pitch is on. ~0.05 is subtle, ~0.2 is obvious.")]
        [SerializeField, Range(0f, 0.5f)] float pitchVariation = 0.1f;
        [Tooltip("When playing from a Position backed by a live object (Transform/GameObject/Component), follow it over time.")]
        [SerializeField] bool followPositionSource = true;

        // The emitter this wrapper started and still "owns". Cleared automatically when the
        // sound ends, so it never goes stale. Not serialized — pure runtime state.
        SoundEmitter current;

        public bool HasClip => data is { clip: not null };
        public bool IsPlaying => current != null;

        /// <summary>Play at a world position (3D). Fluent: returns this so follow-ups can be chained.</summary>
        public SoundEffect Play(Vector3 position) {
            if (!HasClip) return this;

            SoundBuilder builder = SoundManager.Instance.CreateSoundBuilder().WithPosition(position);
            if (randomPitch) builder.WithRandomPitch(pitchVariation);

            SoundEmitter emitter = builder.Play(data);
            if (emitter != null) {
                current = emitter;
                // Clear our reference when THIS instance ends (guard against a newer Play having replaced it).
                emitter.OnComplete += () => { if (current == emitter) current = null; };
            }
            return this;
        }

        /// <summary>
        /// Play at a Position source. If the Position can provide a live Transform, the sound will follow it.
        /// </summary>
        public SoundEffect Play(Position position) => Play(position, followPositionSource);

        public SoundEffect Play(Position position, bool follow) {
            if (!HasClip) return this;

            Vector3 worldPos = position != null ? position.Get() : Vector3.zero;
            SoundBuilder builder = SoundManager.Instance.CreateSoundBuilder().WithPosition(worldPos);

            if (follow && position != null && position.TryGetFollowTarget(out Transform followTarget) && followTarget != null) {
                builder.WithFollowTarget(followTarget);
            }

            if (randomPitch) builder.WithRandomPitch(pitchVariation);

            SoundEmitter emitter = builder.Play(data);
            if (emitter != null) {
                current = emitter;
                emitter.OnComplete += () => { if (current == emitter) current = null; };
            }
            return this;
        }

        /// <summary>Play with no positional data (UI / 2D).</summary>
        public SoundEffect Play() => Play(Vector3.zero);

        /// <summary>Stop the instance this wrapper is currently playing (no-op if nothing is playing). Use for loops.</summary>
        public SoundEffect Stop() {
            if (current != null) current.Stop();
            current = null;
            return this;
        }

        /// <summary>Stop another sound effect and keep this fluent chain alive.</summary>
        public SoundEffect Stop(SoundEffect soundEffect) {
            soundEffect?.Stop();
            return this;
        }

        /// <summary>
        /// After the sound started by the preceding Play FINISHES (or is stopped), play <paramref name="next"/>.
        /// Returns this so more follow-ups can be chained onto the same source.
        /// </summary>
        public SoundEffect PlayOnEnd(SoundEffect next) {
            if (current != null) current.OnComplete += () => next.Play();
            else next.Play(); // head never played — don't break the chain
            return this;
        }
        
        public SoundEffect PlayOnEnd(SoundEffect next, Vector3 position) {
            if (current != null) current.OnComplete += () => next.Play(position);
            else next.Play(position); // head never played — don't break the chain
            return this;
        }

        public SoundEffect PlayOnEnd(SoundEffect next, Position position) {
            if (current != null) current.OnComplete += () => next.Play(position);
            else next.Play(position); // head never played — don't break the chain
            return this;
        }

        public SoundEffect PlayOnEnd(SoundEffect next, Position position, bool follow) {
            if (current != null) current.OnComplete += () => next.Play(position, follow);
            else next.Play(position, follow); // head never played — don't break the chain
            return this;
        }

        /// <summary>
        /// After <paramref name="delay"/> seconds, play <paramref name="next"/> (independent of the head's length).
        /// Good for a follow-up that should land shortly after the previous sound STARTED (e.g. a shotgun pump).
        /// Returns this so more follow-ups can be chained onto the same source.
        /// </summary>
        public SoundEffect PlayDelayed(SoundEffect next, float delay, Vector3 position) {
            // Run on the persistent SoundManager so the delay survives even after our emitter returns to the pool.
            SoundManager.Instance.StartCoroutine(DelayedRoutine(next, delay, position));
            return this;
        }

        public SoundEffect PlayDelayed(SoundEffect next, float delay, Position position) {
            SoundManager.Instance.StartCoroutine(DelayedRoutine(next, delay, position));
            return this;
        }

        public SoundEffect PlayDelayed(SoundEffect next, float delay, Position position, bool follow) {
            SoundManager.Instance.StartCoroutine(DelayedRoutine(next, delay, position, follow));
            return this;
        }

        IEnumerator DelayedRoutine(SoundEffect next, float delay, Vector3 position) {
            yield return new WaitForSeconds(delay);
            next.Play(position);
        }

        IEnumerator DelayedRoutine(SoundEffect next, float delay, Position position) {
            yield return new WaitForSeconds(delay);
            next.Play(position);
        }

        IEnumerator DelayedRoutine(SoundEffect next, float delay, Position position, bool follow) {
            yield return new WaitForSeconds(delay);
            next.Play(position, follow);
        }

        /// <summary>
        /// Play, then force-stop after <paramref name="seconds"/>. Intended for a sustained sound whose
        /// SoundData has loop = true (e.g. an abduction ray) so it fills the whole window; a non-looping
        /// clip just plays once and the timed stop becomes a no-op. Returns this for chaining.
        /// </summary>
        public SoundEffect PlayForSeconds(Vector3 position, float seconds) {
            Play(position);
            SoundEmitter started = current;
            if (started != null) SoundManager.Instance.StartCoroutine(StopAfterSeconds(started, seconds));
            return this;
        }

        public SoundEffect PlayForSeconds(Position position, float seconds) {
            Play(position);
            SoundEmitter started = current;
            if (started != null) SoundManager.Instance.StartCoroutine(StopAfterSeconds(started, seconds));
            return this;
        }

        public SoundEffect PlayForSeconds(Position position, float seconds, bool follow) {
            Play(position, follow);
            SoundEmitter started = current;
            if (started != null) SoundManager.Instance.StartCoroutine(StopAfterSeconds(started, seconds));
            return this;
        }

        IEnumerator StopAfterSeconds(SoundEmitter emitter, float seconds) {
            yield return new WaitForSeconds(seconds);
            if (current == emitter) Stop(); // only stop if we still own that same instance
        }

        /// <summary>
        /// Play a NON-looping clip <paramref name="count"/> times back-to-back. Each repeat starts when the
        /// previous one ends (so random pitch re-rolls per repeat). Returns this for chaining.
        /// </summary>
        public SoundEffect PlayTimes(Vector3 position, int count) {
            if (count <= 0) return this;

            Play(position);
            if (count > 1 && current != null) {
                current.OnComplete += () => PlayTimes(position, count - 1);
            }
            return this;
        }

        public SoundEffect PlayTimes(Position position, int count) {
            if (count <= 0) return this;

            Play(position);
            if (count > 1 && current != null) {
                current.OnComplete += () => PlayTimes(position, count - 1);
            }
            return this;
        }

        public SoundEffect PlayTimes(Position position, int count, bool follow) {
            if (count <= 0) return this;

            Play(position, follow);
            if (count > 1 && current != null) {
                current.OnComplete += () => PlayTimes(position, count - 1, follow);
            }
            return this;
        }
    }
}

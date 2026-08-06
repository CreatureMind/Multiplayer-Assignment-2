using UnityEngine;
using Random = UnityEngine.Random;

namespace AudioSystem {
    public static class SoundEffectExtensions {
        /// <summary>
        /// Play one randomly-chosen effect from the array at a world position — great for varied
        /// footsteps, impacts, etc. Returns the chosen <see cref="SoundEffect"/> so it stays fluent
        /// (chain PlayOnEnd / PlayDelayed / Stop), or null if the array is null/empty.
        /// A <c>Position</c> implicitly converts to the Vector3 parameter.
        /// </summary>
        public static SoundEffect PlayRandom(this SoundEffect[] effects, Vector3 position) {
            if (effects == null || effects.Length == 0) {
                Debug.LogWarning("PlayRandom called on an empty SoundEffect array.");
                return null;
            }

            SoundEffect chosen = effects[Random.Range(0, effects.Length)];
            return chosen.Play(position);
        }

        public static SoundEffect PlayRandom(this SoundEffect[] effects, Position position) {
            if (effects == null || effects.Length == 0) {
                Debug.LogWarning("PlayRandom called on an empty SoundEffect array.");
                return null;
            }

            SoundEffect chosen = effects[Random.Range(0, effects.Length)];
            return chosen.Play(position);
        }

        public static SoundEffect PlayRandom(this SoundEffect[] effects, Position position, bool follow) {
            if (effects == null || effects.Length == 0) {
                Debug.LogWarning("PlayRandom called on an empty SoundEffect array.");
                return null;
            }

            SoundEffect chosen = effects[Random.Range(0, effects.Length)];
            return chosen.Play(position, follow);
        }

        /// <summary>Play one randomly-chosen effect with no positional data (UI / 2D).</summary>
        public static SoundEffect PlayRandom(this SoundEffect[] effects) => effects.PlayRandom(Vector3.zero);
    }
}

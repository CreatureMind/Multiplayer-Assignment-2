using UnityEngine;

namespace AudioSystem {
    /// <summary>
    /// Drop-in component that exposes up to three role-based sounds to the inspector.
    /// The public Play methods take no arguments, so they can be wired directly into
    /// UnityEvents, Animation Events, and Button OnClick callbacks.
    ///
    /// Any slot left empty is safely ignored (e.g. leave <c>secondary</c> empty on
    /// objects that never get hit).
    /// </summary>
    public class AudioEmitter : MonoBehaviour {
        [Header("Primary — the action (click, spawn, fire)")]
        [SerializeField] SoundEffect primary;
        [Header("Secondary — impact / hit")]
        [SerializeField] SoundEffect secondary;
        [Header("Tertiary — aftermath (explosion, etc.)")]
        [SerializeField] SoundEffect tertiary;

        public void PlayPrimary() => primary.Play(transform.position);
        public void PlaySecondary() => secondary.Play(transform.position);
        public void PlayTertiary() => tertiary.Play(transform.position);
    }
}

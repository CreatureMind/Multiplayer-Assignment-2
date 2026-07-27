using System;
using AudioSystem;
using UnityEngine;

public class AmbientSoundsHandler : MonoBehaviour
{
    [Header("Sound Effects")]
    [SerializeField] private SoundEffect dayAmbientSound;
    [SerializeField] private SoundEffect nightAmbientSound;
    [SerializeField] private SoundEffect roosterSound;
    [SerializeField] private SoundEffect owlSound;
    [SerializeField] private SoundEffect popSound;
    
    [Header("Theme")]
    [SerializeField] private AudioClip dayThemeMusic;
    [SerializeField] private AudioClip nightThemeMusic;
    
    [Header("Music Manager")]
    [SerializeField] private MusicManager musicManager;


    private void Awake()
    {
        if (!musicManager)
            musicManager = MusicManager.Instance;
    }

    private void OnEnable()
    {
        musicManager.AddToPlaylist(dayThemeMusic);
        musicManager.SetVolume(0.5f);
        
    }

    private void OnDisable()
    {
    }

    private void Start()
    {
        if (!musicManager)
            Debug.LogError("MusicManager is not assigned in the inspector.");
    }

    private void HandleSunrise()
    {
        roosterSound.Play().PlayOnEnd(dayAmbientSound).Stop(nightAmbientSound);
        musicManager.AddToPlaylist(dayThemeMusic);
        musicManager.SetVolume(0.5f);
        musicManager.PlayNextTrack();
    }

    private void HandleNightfall()
    {
        owlSound.Play().PlayOnEnd(nightAmbientSound).Stop(dayAmbientSound);
        musicManager.AddToPlaylist(nightThemeMusic);
        musicManager.SetVolume(0.2f);
        musicManager.PlayNextTrack();
    }
}

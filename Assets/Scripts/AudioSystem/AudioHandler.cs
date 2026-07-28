using System;
using AudioSystem;
using UnityEngine;

public class AudioHandler : MonoBehaviour
{
    [Header("UI Sound Effects")]
    [SerializeField] private SoundEffect btnHoverSound;
    [SerializeField] private SoundEffect btnClickSound;
    [SerializeField] private SoundEffect loadingSound;
    
    [Header("Game Sounds effects")]
    [SerializeField] private SoundEffect pawnSpawnSound;
    [SerializeField] private SoundEffect pawnExplosionSound;
    [SerializeField] private SoundEffect pawnEatenSound;
    
    [SerializeField] private SoundEffect basePlacementSound;
    [SerializeField] private SoundEffect yourBaseConqueredSound;
    [SerializeField] private SoundEffect enemyBaseConqueredSound;
    
    [SerializeField] private SoundEffect loseSound;
    [SerializeField] private SoundEffect winSound;
    
    [Header("Theme")]
    [SerializeField] private AudioClip lobbyThemeMusic;
    [SerializeField] private AudioClip gameThemeMusic;
    
    [Header("Music Manager")]
    [SerializeField] private MusicManager musicManager;


    private void Awake()
    {
        if (!musicManager)
            musicManager = MusicManager.Instance;
    }

    private void OnEnable()
    {
        musicManager.AddToPlaylist(lobbyThemeMusic);
        musicManager.SetVolume(0.5f);
        
    }

    private void Start()
    {
        if (!musicManager)
            Debug.LogError("MusicManager is not assigned in the inspector.");
    }

    private void HandleLobbyMusic()
    {
        musicManager.AddToPlaylist(lobbyThemeMusic);
        musicManager.SetVolume(0.5f);
        musicManager.PlayNextTrack();
    }

    private void HandleGameMusic()
    {
        musicManager.AddToPlaylist(gameThemeMusic);
        musicManager.SetVolume(0.5f);
        musicManager.PlayNextTrack();
    }
}

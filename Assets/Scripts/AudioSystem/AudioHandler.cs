using System;
using AudioSystem;
using Events;
using UnityEngine;

public enum SoundEffectEnum
{
    BTN_HOVER,
    BTN_CLICK,
    LOADING_START,
    LOADING_END,
    PAWN_SPAWN,
    PAWN_EXPLOSION,
    PAWN_EATEN,
    BASE_PLACEMENT,
    YOUR_BASE_CONQUERED,
    ENEMY_BASE_CONQUERED,
    LOSE,
    WIN
}

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
        
        EventBus.Subscribe<PlaySoundEvent>(HandleSounds);
        EventBus.Subscribe<JoinedLobbyEvent>(HandleLobbyMusic);
        EventBus.Subscribe<GameSceneLoadedEvent>(HandleGameMusic);
    }
    
    private void OnDisable()
    {
        EventBus.Unsubscribe<PlaySoundEvent>(HandleSounds);
        EventBus.Unsubscribe<JoinedLobbyEvent>(HandleLobbyMusic);
        EventBus.Unsubscribe<GameSceneLoadedEvent>(HandleGameMusic);
    }

    private void HandleSounds(PlaySoundEvent obj)
    {
        switch (obj.SoundName)
        {
            case SoundEffectEnum.BTN_HOVER:
                HandleHover();
                break;
            case SoundEffectEnum.BTN_CLICK:
                HandleClick();
                break;
            case SoundEffectEnum.LOADING_START:
                HandleLoading(SoundEffectEnum.LOADING_START);
                break;
            case SoundEffectEnum.LOADING_END:
                HandleLoading(SoundEffectEnum.LOADING_END);
                break;
            case SoundEffectEnum.PAWN_SPAWN:
                HandleSpawn();
                break;
            case SoundEffectEnum.PAWN_EXPLOSION:
                HandleExplosion();
                break;
            case SoundEffectEnum.PAWN_EATEN:
                HandleEaten();
                break;
            case SoundEffectEnum.BASE_PLACEMENT:
                HandleBasePlacement();
                break;
            case SoundEffectEnum.YOUR_BASE_CONQUERED:
                HandleBaseConqueredYour();
                break;
            case SoundEffectEnum.ENEMY_BASE_CONQUERED:
                HandleBaseConqueredEnemy();
                break;
            case SoundEffectEnum.LOSE:
                HandleLose();
                break;
            case SoundEffectEnum.WIN:
                HandleWin();
                break;
            default:
                Debug.LogWarning("Sound effect not found");
                break;
        }
    }
    
    private void Start()
    {
        if (!musicManager)
            Debug.LogError("MusicManager is not assigned in the inspector.");
    }

    private void HandleLobbyMusic(JoinedLobbyEvent e)
    {
        if(!NetworkManager.Instance.IsReturningFromMatch) return;
        
        musicManager.AddToPlaylist(lobbyThemeMusic);
        musicManager.SetVolume(0.5f);
        musicManager.PlayNextTrack();
    }

    private void HandleGameMusic(GameSceneLoadedEvent e)
    {
        musicManager.AddToPlaylist(gameThemeMusic);
        musicManager.SetVolume(0.5f);
        musicManager.PlayNextTrack();
    }
    
    private void HandleHover()
    {
        btnHoverSound.Play();
    }

    private void HandleClick()
    {
        btnClickSound.Play();
    }

    private void HandleLoading(SoundEffectEnum toggle)
    {
        if (toggle == SoundEffectEnum.LOADING_START)
            loadingSound.Play();
        else
            loadingSound.Stop();
    }

    private void HandleSpawn()
    {
        pawnSpawnSound.Play();
    }

    private void HandleExplosion()
    {
        pawnExplosionSound.Play();
    }

    private void HandleEaten()
    {
        pawnEatenSound.Play();
    }

    private void HandleBasePlacement()
    {
        basePlacementSound.Play();
    }

    private void HandleBaseConqueredYour()
    {
        yourBaseConqueredSound.Play();
    }

    private void HandleBaseConqueredEnemy()
    {
        enemyBaseConqueredSound.Play();
    }

    private void HandleLose()
    {
        loseSound.Play();
    }

    private void HandleWin()
    {
        winSound.Play();
    }
}

using System;
    using Fusion;
    using UnityEngine;
    
    public class ScoreManager : NetworkBehaviour, IPlayerLeft
    {
        public static ScoreManager Instance { get; private set; }
    
        public static event Action OnScoresChanged;
    
        [Networked, Capacity(MaxPlayers)] private NetworkDictionary<PlayerRef, int> PlayerScores => default;
    
        private ChangeDetector _changeDetector;
    
        const int MaxPlayers = 10;
        const int ScoreToAdd = 10;
        const int DefaultScore = 0;
    
        public override void Spawned()
        {
            if (Instance != null && Instance != this)
            {
                Runner.Despawn(Object);
                return;
            }
    
            Instance = this;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }
    
        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(PlayerScores))
                {
                    OnScoresChanged?.Invoke();
                }
            }
        }
    
        /// <summary>
        /// Callback from IPlayerLeft. Cleans up leaving player's score on the State Authority.
        /// </summary>
        public void PlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
    
            if (PlayerScores.ContainsKey(player))
            {
                PlayerScores.Remove(player);
            }
        }
    
        public void AddScore(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
    
            if (PlayerScores.ContainsKey(player))
            {
                PlayerScores.Set(player, PlayerScores[player] + ScoreToAdd);
            }
            else
            {
                PlayerScores.Add(player, ScoreToAdd);
            }
        }
    
        public int GetScore(PlayerRef player)
        {
            return PlayerScores.ContainsKey(player) ? PlayerScores.Get(player) : DefaultScore;
        }
    
        public void ResetPlayerScore(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
    
            if (PlayerScores.ContainsKey(player))
            {
                PlayerScores.Set(player, 0);
            }
        }
    }
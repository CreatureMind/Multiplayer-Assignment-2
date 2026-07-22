
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "GameDataSO", menuName = "ScriptableObjects/GameDataSO", order = 1)]
    public class GameDataSO : ScriptableObject 
    
    // the idea is to possibly make a couple for each gamemode
    {
        [SerializeField] private ClientManager  clientManagerPrefab;
        [SerializeField] private BoardManager boardManagerPrefab;
        [SerializeField] private List<int> numberOfPlayers;
        
        public bool ValidatePlayerCount(int playerCount)
        {
            return numberOfPlayers.Contains(playerCount);
        }
        
        public ClientManager ClientManagerPrefab => clientManagerPrefab;
        public BoardManager BoardManagerPrefab => boardManagerPrefab;
        
        public List<int> NumberOfPlayers => numberOfPlayers;
    }


    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Serialization;

    [CreateAssetMenu(fileName = "GameDataSO", menuName = "ScriptableObjects/GameDataSO", order = 1)]
    public class GameDataSO : ScriptableObject 
    
    // the idea is to possibly make a couple for each gamemode
    {
        [SerializeField] private ClientManager  clientManagerPrefab;
        [SerializeField] private BoardManager boardManagerPrefab;
        [SerializeField] private TurnManager turnManagerPrefab;
        [SerializeField] private List<int> numberOfPlayersToEnforce;
        [SerializeField] private StartingPositionCatalogSO mapCatalog;
        [SerializeField] private TurnStatsSO turnStats;
        public bool ValidatePlayerCount(int playerCount)
        {
            return numberOfPlayersToEnforce.Contains(playerCount);
        }
        
        public ClientManager ClientManagerPrefab => clientManagerPrefab;
        public BoardManager BoardManagerPrefab => boardManagerPrefab;
        public TurnManager TurnManagerPrefab => turnManagerPrefab;
        public List<int> NumberOfPlayersToEnforce => numberOfPlayersToEnforce;
        public StartingPositionCatalogSO MapCatalog => mapCatalog;
        public TurnStatsSO TurnStats => turnStats;
    }

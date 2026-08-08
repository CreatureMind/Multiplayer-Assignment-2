using UnityEngine;

[CreateAssetMenu(fileName = "NewStartingPositionCatalog", menuName = "ScriptableObjects/StartingPositionCatalogSO", order = 1)]
public class StartingPositionCatalogSO : ScriptableObject
{
    [SerializeField] private StartingPositionSO[] startingPositions;
    
    public StartingPositionSO GetMapByString(string mapName)
    {
        foreach (var startingPosition in startingPositions)
        {
            if (startingPosition.MapName == mapName)
            {
                return startingPosition;
            }
        }
        Debug.LogWarning($"[StartingPositionCatalogSO] Map with name '{mapName}' not found.");
        return null;
    }
}

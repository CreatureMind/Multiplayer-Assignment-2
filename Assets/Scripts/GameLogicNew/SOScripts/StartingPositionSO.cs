using System;
using UnityEngine;

[Serializable]
public struct AuthoredTile
{
    public TileType type;
    public byte ownerId;
    public short territoryId;
    
    public AuthoredTile(TileType type, byte ownerId, short territoryId)
    {
        this.type = type;
        this.ownerId = ownerId;
        this.territoryId = territoryId;
    }

    public TileState ToTileState() => new TileState(type, ownerId, territoryId);
    public static AuthoredTile From(in TileState s) => new AuthoredTile(s.Type, s.OwnerId, s.TerritoryId);
}

[CreateAssetMenu(fileName = "NewStartingPosition", menuName = "ScriptableObjects/StartingPositionSO", order = 1)]
public class StartingPositionSO : ScriptableObject
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private AuthoredTile[] startingPosition;

    public int Width => width;
    public int Height => height;
    
    public TileState[] BuildTileStates()
    {
        var count = Mathf.Max(0, width * height);
        var result = new TileState[count];
        for (var i = 0; i < count; i++)
            result[i] = startingPosition != null && i < startingPosition.Length
                ? startingPosition[i].ToTileState()
                : TileState.Empty;
        return result;
    }

    public TileState GetTileState(int x, int y)
    {
        var index = y * width + x;
        return startingPosition != null && index < startingPosition.Length
            ? startingPosition[index].ToTileState()
            : TileState.Empty;
    }
}
using UnityEngine;

[CreateAssetMenu(fileName = "NewStartingPosition", menuName = "ScriptableObjects/StartingPositionSO", order = 1)]
public class StartingPositionSO : ScriptableObject
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private TileState[] startingPosition;

    public int Width => width;
    public int Height => height;
    public TileState[] StartingPosition => startingPosition;
}
using UnityEngine;


[CreateAssetMenu(fileName = "TurnStatsSO", menuName = "ScriptableObjects/TurnStatsSO", order = 1)]
public class TurnStatsSO : ScriptableObject
{
    // might want to reduce to 0 depending on the implementation of base and the initialization of the map
    [SerializeField] private int initialActionAmount;
    
    [SerializeField] private int actionGainPerBase;
    [SerializeField] private int pawnActionPrice;
    [SerializeField] private int bombActionPrice;
    
    public int InitialActionAmount => initialActionAmount;
    public int ActionGainPerBase => actionGainPerBase;
    public int PawnActionPrice => pawnActionPrice;
    public int BombActionPrice => bombActionPrice;
}

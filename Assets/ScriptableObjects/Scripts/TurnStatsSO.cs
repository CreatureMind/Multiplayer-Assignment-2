using UnityEngine;


[CreateAssetMenu(fileName = "TurnStatsSO", menuName = "ScriptableObjects/TurnStatsSO", order = 1)]
public class TurnStatsSO : ScriptableObject
{
    [SerializeField] private int actionGainPerBase;
    [SerializeField] private int pawnActionPrice;
    [SerializeField] private int bombActionPrice;
    
    public int ActionGainPerBase => actionGainPerBase;
    public int PawnActionPrice => pawnActionPrice;
    public int BombActionPrice => bombActionPrice;
}

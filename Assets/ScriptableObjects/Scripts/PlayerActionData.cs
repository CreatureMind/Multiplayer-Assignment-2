using Fusion;

public struct PlayerActionData : INetworkStruct
{
    public int PlayerId;
    public int MaxActionAmountPerTurn;
    public int CurrentActionAmount;

    public PlayerActionData(int maxActionAmountPerTurn, int playerId)
    {
        PlayerId = playerId;
        MaxActionAmountPerTurn = maxActionAmountPerTurn;
        CurrentActionAmount = maxActionAmountPerTurn;
    }
    
    public void ResetCurrentActionAmount()
    {
        CurrentActionAmount = MaxActionAmountPerTurn;
    }

    public void UpdateMaxActionAmountPerTurn(int amountToAdd)
    {
        MaxActionAmountPerTurn += amountToAdd;
    }

    public void UpdateCurrentActionAmount(int amountToReduce)
    {
        CurrentActionAmount -= amountToReduce;
        if (CurrentActionAmount < 0)
            CurrentActionAmount = 0;
    }
    
    public bool HasEnoughToBuildBase() // needs to be changed if any other base building conditions are wanted
    {
        return CurrentActionAmount == MaxActionAmountPerTurn;
    }
    
    public bool HasEnoughToPlaceBomb(int priceOfBomb)
    {
        return CurrentActionAmount >= priceOfBomb;
    }
    
    public bool HasEnoughToPlacePawn(int priceOfPawn)
    {
        return CurrentActionAmount >= priceOfPawn;
    }
    
    public bool TurnEnded()
    {
        return CurrentActionAmount == 0;
    }
}
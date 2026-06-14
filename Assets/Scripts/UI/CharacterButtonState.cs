using Fusion;
using UnityEngine;

public enum CharacterSlotState
{
    Available,
    TakenByOther,
    TakenBySelf
}

public struct CharacterButtonState
{
    public int CharacterId;
    public string CharacterName;
    public Color CharacterColor;
    public CharacterSlotState SlotState;
    public bool IsInteractable => SlotState == CharacterSlotState.Available;
}
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character Definition", fileName = "CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    [field: SerializeField] public int CharacterId { get; private set; }
    
    [field: SerializeField] public string CharacterName { get; private set; }
    
    [field: SerializeField] public Color CharacterColor { get; private set; }
    
    //TODO: replace with the actual player prefab once the spawning exists
    // [field: SerializeField] public NetworkObject CharacterPrefab { get; private set; }
}
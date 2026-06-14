using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character Registry", fileName = "CharacterRegistry")]
public class CharacterRegistry : ScriptableObject
{
    [SerializeField] private List<CharacterDefinition> characters = new();
    
    public IReadOnlyList<CharacterDefinition> Characters => characters;
    
    public CharacterDefinition GetById(int characterId)
    {
        foreach (var def in characters.Where(def => def.CharacterId == characterId))
            return def;

        Debug.LogWarning($"[CharacterRegistry] No character found with id {characterId}");
        return null;
    }
}
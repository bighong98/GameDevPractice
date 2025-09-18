using UnityEngine;
using RPG.Stats;

public abstract class CharacterTypeSO : BaseTypeSO
{
    [Header("Character")] 
    public CharacterClass characterClass;
    public float height;
    public int startingLevel;
}

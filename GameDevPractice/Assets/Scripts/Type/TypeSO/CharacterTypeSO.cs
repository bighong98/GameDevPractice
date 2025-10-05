using UnityEngine;
using RPG.Stats;
using UnityEngine.Serialization;
using TH.Attribute.Stat;

public abstract class CharacterTypeSO : BaseTypeSO
{
    [Header("Character")] 
    public CharacterType characterType;
    public BaseStatListSO characterBaseStats;
    public float height;
    public int startingLevel;
}

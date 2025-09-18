using UnityEngine;

[CreateAssetMenu(fileName = "PlayerTypeSO", menuName = "Scriptable Objects/Type/Character/PlayerTypeSO")]
public class PlayerTypeSO : CharacterTypeSO
{
    [Header("Player")] 
    public GameObject levelUpEffect;
    
}

using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "PlayerTypeSO", menuName = "Scriptable Objects/Type/Character/PlayerTypeSO")]
    public class PlayerTypeSO : CharacterTypeSO
    {
        [Header("Player")] 
        public GameObject levelUpEffect;
    
    }
}


using TH.Attribute;
using UnityEngine;

namespace TH.Attribute
{
    [CreateAssetMenu(fileName = "DamageRuleSO", menuName = "Scriptable Objects/Combat/CombatRuleSO/DamageRuleSO")]
    public class DamageRuleSO : ScriptableObject
    {
        [Header("Defence Stats Mapping")]
        [SerializeField] private DefenceStatRuleSO defenceStatRuleSO;
        public DefenceStatRuleSO DefenceStatRuleSO => defenceStatRuleSO;

        [Header("Calculation Constants")]
        private float defenseConstant = 100f; // 방어력(저항력) 상수

        public float GetMultiplierByDefenseStat(float defenseValue)
        {
            if (defenseValue <= 0) return 1f;
            return defenseConstant / (defenseConstant + defenseValue); 
        }
    }
}


using TH.Attribute.Stat;
using TH.Combat;
using TH.Utils;
using UnityEngine;

namespace TH.Attribute
{
    [CreateAssetMenu(fileName = "DefenceStatRuleSO", menuName = "Scriptable Objects/Combat/CombatRuleSO/DefenceStatRuleSO")]
    public class DefenceStatRuleSO : KeyValueListSO<DamageType, GameStatSO>
    {
        
    }
}


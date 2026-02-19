using TH.Combat.Service;
using UnityEngine;

namespace TH.Combat
{
    public abstract class SkillOnHitEffectSO : ScriptableObject
    {
        public abstract void Execute(in SkillOnHitContext context, ICombatSystem combatSystem);
    }
}
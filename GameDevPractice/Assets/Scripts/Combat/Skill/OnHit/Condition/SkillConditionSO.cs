using UnityEngine;

namespace TH.Combat
{
    public abstract class SkillConditionSO : ScriptableObject
    {
        public abstract bool Evaluate(in SkillOnHitContext context);
    }
}

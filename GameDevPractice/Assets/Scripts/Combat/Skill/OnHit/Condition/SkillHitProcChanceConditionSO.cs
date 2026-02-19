using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillHitProcChanceConditionSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/Condition/Chance")]
    public sealed class SkillHitProcChanceConditionSO : SkillConditionSO
    {
        [SerializeField, Range(0f, 1f)] private float chance = 1f;

        public override bool Evaluate(in SkillOnHitContext context)
        {
            _ = context;

            if (chance <= 0f)
            {
                return false;
            }

            if (chance >= 1f)
            {
                return true;
            }

            return Random.value <= chance;
        }
    }
}

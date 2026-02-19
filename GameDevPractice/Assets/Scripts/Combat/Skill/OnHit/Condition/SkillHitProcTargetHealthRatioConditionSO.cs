using TH.Attribute;
using UnityEngine;

namespace TH.Combat
{
    public enum SkillHitProcHealthRatioComparison
    {
        LessOrEqual,
        GreaterOrEqual,
    }

    [CreateAssetMenu(fileName = "SkillHitProcTargetHealthRatioConditionSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/Condition/TargetHealthRatio")]
    public sealed class SkillHitProcTargetHealthRatioConditionSO : SkillConditionSO
    {
        [SerializeField] private SkillHitProcHealthRatioComparison comparison = SkillHitProcHealthRatioComparison.LessOrEqual;
        [SerializeField, Range(0f, 1f)] private float threshold = 0.5f;

        public override bool Evaluate(in SkillOnHitContext context)
        {
            if (context.PrimaryTarget.IsNull())
            {
                return false;
            }

            float maxHp = Mathf.Max(0f, context.PrimaryTarget.MaxHp);
            if (maxHp <= 0f)
            {
                return false;
            }

            float ratio = Mathf.Clamp01(context.PrimaryTarget.Hp / maxHp);
            return comparison == SkillHitProcHealthRatioComparison.LessOrEqual
                ? ratio <= threshold
                : ratio >= threshold;
        }
    }
}

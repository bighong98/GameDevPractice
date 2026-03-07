using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "DirectHitSkillActionSO", menuName = "Scriptable Objects/Combat/Skill/Action/DirectHit")]
    public class DirectHitSkillActionSO : SkillActionSO
    {
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;
        [SerializeField] private bool recheckRangeAtHitTiming;

        private const float RangeCompareBuffer = 0.5f;

        protected override void ExecuteOnce(SkillExecutionContext context, ISkillExecutionServices services, int iteration, int totalIterations)
        {
            if (recheckRangeAtHitTiming && !IsPrimaryTargetInRange(context))
            {
                return;
            }

            services.TryApplyHit(context, context.PrimaryTarget, damageScale, hitCountOverride);
        }

        private bool IsPrimaryTargetInRange(in SkillExecutionContext context)
        {
            var target = context.PrimaryTarget;
            if (target == null)
            {
                return false;
            }

            if (context.Attacker is not Component attackerComponent)
            {
                return true;
            }

            float maxRange = ResolveSkillRange(context) + RangeCompareBuffer;
            Vector3 offset = target.transform.position - attackerComponent.transform.position;
            return offset.sqrMagnitude <= maxRange * maxRange;
        }

        private static float ResolveSkillRange(in SkillExecutionContext context)
        {
            if (context.RuntimeSkill != null)
            {
                return Mathf.Max(0f, context.RuntimeSkill.Range);
            }

            return context.Skill != null
                ? Mathf.Max(0f, context.Skill.Range)
                : 0f;
        }
    }
}

using TH.Attribute;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "AreaHitSkillActionSO", menuName = "Scriptable Objects/Combat/Skill/Action/AreaHit")]
    public class AreaHitSkillActionSO : SkillActionSO
    {
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField, Min(1)] private int maxTargets = 16;
        [SerializeField] private bool includePrimaryTarget = true;
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;

        protected override void ExecuteOnce(SkillExecutionContext context, ISkillExecutionServices services, int iteration, int totalIterations)
        {
            if (context.PrimaryTarget.IsNull())
            {
                return;
            }

            var center = context.PrimaryTarget.transform.position;
            var targets = services.FindTargetsInRadius(
                center,
                radius,
                Mathf.Max(1, maxTargets),
                context.PrimaryTarget,
                includePrimaryTarget);

            for (int i = 0; i < targets.Count; i++)
            {
                services.TryApplyHit(context, targets[i], damageScale, hitCountOverride);
            }
        }
    }
}

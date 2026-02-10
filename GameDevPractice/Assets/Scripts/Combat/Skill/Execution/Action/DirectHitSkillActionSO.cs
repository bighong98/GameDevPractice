using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "DirectHitSkillActionSO", menuName = "Scriptable Objects/Combat/Skill/Action/DirectHit")]
    public class DirectHitSkillActionSO : SkillActionSO
    {
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;

        protected override void ExecuteOnce(SkillExecutionContext context, ISkillExecutionServices services, int iteration, int totalIterations)
        {
            services.TryApplyHit(context, context.PrimaryTarget, damageScale, hitCountOverride);
        }
    }
}

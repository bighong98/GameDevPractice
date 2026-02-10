using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "ProjectileBurstSkillActionSO", menuName = "Scriptable Objects/Combat/Skill/Action/ProjectileBurst")]
    public class ProjectileBurstSkillActionSO : SkillActionSO
    {
        [SerializeField, Min(1)] private int projectileCount = 1;
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;

        protected override void ExecuteOnce(SkillExecutionContext context, ISkillExecutionServices services, int iteration, int totalIterations)
        {
            int spawnCount = Mathf.Max(1, projectileCount);
            for (int i = 0; i < spawnCount; i++)
            {
                services.TryLaunchProjectile(context, context.PrimaryTarget, damageScale, hitCountOverride);
            }
        }
    }
}

using System.Collections.Generic;
using TH.Attribute;

namespace TH.Combat
{
    public interface ISkillExecutionServices
    {
        bool TryApplyHit(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0);
        bool TryLaunchProjectile(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0);
        IReadOnlyList<Health> FindTargetsInRadius(SkillExecutionContext context, UnityEngine.Vector3 center, float radius, int maxTargets, Health primaryTarget, bool includePrimary);
    }
}

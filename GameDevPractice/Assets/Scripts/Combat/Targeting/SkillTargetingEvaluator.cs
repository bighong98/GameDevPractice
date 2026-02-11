using TH.Attribute;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    public sealed class SkillTargetingEvaluator
    {
        private readonly SkillTargetLayerMaskResolver layerMaskResolver;

        public SkillTargetingEvaluator(SkillTargetLayerMaskResolver layerMaskResolver)
        {
            this.layerMaskResolver = layerMaskResolver;
        }

        public int ResolveTargetLayerMask(in SkillExecutionContext context)
        {
            if (context.Attacker.IsNull() || context.Skill.IsNull())
                return 0;

            return layerMaskResolver.Resolve(context.Attacker, context.Skill.TargetPolicy);
        }

        public bool CanTarget(in SkillExecutionContext context, Health target)
        {
            if (context.Attacker.IsNull() || context.Skill.IsNull() || target.IsNull())
                return false;

            var policy = context.Skill.TargetPolicy;

            if (!policy.AllowSelf && IsSelf(context.Attacker, target))
                return false;

            if (!policy.IncludeDeadTargets && target.IsDead)
                return false;

            int targetLayer = target.gameObject.layer;
            if (targetLayer < 0 || targetLayer > 31)
                return false;

            int mask = layerMaskResolver.Resolve(context.Attacker, policy);
            if (mask == 0)
                return false;

            return (mask & (1 << targetLayer)) != 0;
        }

        private static bool IsSelf(IAttacker attacker, Health target)
        {
            if (attacker is not Component attackerComponent)
                return false;

            return ReferenceEquals(attackerComponent.gameObject, target.gameObject);
        }
    }
}

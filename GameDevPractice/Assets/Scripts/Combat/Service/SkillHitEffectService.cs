using UnityEngine;

namespace TH.Combat.Service
{
    public sealed class SkillHitEffectService : ISkillHitEffectService
    {
        public SkillHitEffectService(ICombatSystem combatSystem)
        {
            if (combatSystem != null)
            {
                combatSystem.OnHitApplied += HandleHitApplied;
            }
        }

        private static void HandleHitApplied(in HitResult result, IDamageable target)
        {
            if (result.Skill == null)
            {
                return;
            }

            var targetComponent = target as Component;
            var effectContext = new SkillEffectPlayContext(
                result.Attacker as Component,
                targetComponent,
                result.HitPoint,
                result.HasHitPoint,
                result.AttackInstanceId);
            if (SkillEffectPlayer.TryPlaySkillEffect(result.Skill, SkillEffectTrigger.OnHit, effectContext))
            {
                return;
            }
        }
    }
}

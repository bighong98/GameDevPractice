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
            if (result.Skill == null || !result.Skill.HasOnHitEffect)
            {
                return;
            }

            if (result.HasHitPoint)
            {
                SkillEffectPlayer.TryPlayOnHitEffect(result.Skill, result.HitPoint);
                return;
            }

            if (target is not Component targetComponent)
            {
                return;
            }

            var resolvedPosition = ResolveHitEffectPosition(targetComponent);
            SkillEffectPlayer.TryPlayOnHitEffect(result.Skill, resolvedPosition);
        }


        private static Vector3 ResolveHitEffectPosition(Component targetComponent)
        {
            if (targetComponent == null)
            {
                return default;
            }

            if (targetComponent.TryGetComponent<Collider>(out var collider))
            {
                return collider.bounds.center;
            }

            if (targetComponent.TryGetComponent<Renderer>(out var renderer))
            {
                return renderer.bounds.center;
            }

            return targetComponent.transform.position;
        }
    }
}

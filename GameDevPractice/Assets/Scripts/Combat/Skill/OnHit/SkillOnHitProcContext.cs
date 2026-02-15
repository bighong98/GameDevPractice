using TH.Attribute;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public readonly struct SkillOnHitProcContext
    {
        public readonly HitResult HitResult;
        public readonly Health PrimaryTarget;
        public readonly Vector3 HitPosition;
        public readonly bool HasHitPosition;

        public IAttacker Attacker => HitResult.Attacker;
        public SkillTypeSO SourceSkill => HitResult.Skill;
        public int AttackInstanceId => HitResult.AttackInstanceId;
        public float AppliedDamage => HitResult.Damage;

        public SkillOnHitProcContext(in HitResult hitResult, Health primaryTarget)
        {
            HitResult = hitResult;
            PrimaryTarget = primaryTarget;

            if (hitResult.HasHitPoint)
            {
                HitPosition = hitResult.HitPoint;
                HasHitPosition = true;
                return;
            }

            if (primaryTarget.IsNotNull())
            {
                HitPosition = ResolveFallbackHitPosition(primaryTarget);
                HasHitPosition = true;
                return;
            }

            HitPosition = default;
            HasHitPosition = false;
        }

        private static Vector3 ResolveFallbackHitPosition(Health target)
        {
            if (target == null)
            {
                return default;
            }

            if (target.TryGetComponent<Collider>(out var collider))
            {
                return collider.bounds.center;
            }

            if (target.TryGetComponent<Renderer>(out var renderer))
            {
                return renderer.bounds.center;
            }

            return target.transform.position;
        }
    }
}
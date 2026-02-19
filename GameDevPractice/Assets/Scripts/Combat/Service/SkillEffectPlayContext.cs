using UnityEngine;

namespace TH.Combat.Service
{
    public readonly struct SkillEffectPlayContext
    {
        public readonly Component AttackerComponent;
        public readonly Component TargetComponent;
        public readonly Vector3 HitPoint;
        public readonly bool HasHitPoint;
        public readonly int AttackInstanceId;

        public SkillEffectPlayContext(
            Component attackerComponent,
            Component targetComponent,
            Vector3 hitPoint,
            bool hasHitPoint,
            int attackInstanceId)
        {
            AttackerComponent = attackerComponent;
            TargetComponent = targetComponent;
            HitPoint = hitPoint;
            HasHitPoint = hasHitPoint;
            AttackInstanceId = attackInstanceId;
        }
    }
}

using System.Collections.Generic;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public readonly struct HitRequest
    {
        public readonly IAttacker Attacker;
        public readonly float BaseDamage;
        public readonly IDamageable Target;
        public readonly DamageType DamageType;
        public readonly int AttackInstanceId;
        public readonly IReadOnlyList<float> HitDamages;
        public readonly SkillTypeSO Skill;
        public readonly Vector3 HitPoint;
        public readonly bool HasHitPoint;

        public HitRequest(
            in IAttacker attacker,
            in float baseDamage,
            in IDamageable target,
            in DamageType damageType,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null,
            SkillTypeSO skill = null,
            Vector3 hitPoint = default,
            bool hasHitPoint = false)
        {
            this.Attacker = attacker;
            this.BaseDamage = baseDamage;
            this.Target = target;
            this.DamageType = damageType;
            this.AttackInstanceId = attackInstanceId;
            this.HitDamages = hitDamages;
            this.Skill = skill;
            this.HitPoint = hitPoint;
            this.HasHitPoint = hasHitPoint;
        }
    }
}


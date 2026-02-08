using System.Collections.Generic;
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

        public HitRequest(
            in IAttacker attacker,
            in float baseDamage,
            in IDamageable target,
            in DamageType damageType,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null)
        {
            this.Attacker = attacker;
            this.BaseDamage = baseDamage;
            this.Target = target;
            this.DamageType = damageType;
            this.AttackInstanceId = attackInstanceId;
            this.HitDamages = hitDamages;
        }
    }
}


using UnityEngine;

namespace TH.Combat
{
    public readonly struct HitRequest
    {
        public readonly IAttacker Attacker;
        public readonly float BaseDamage;
        public readonly IDamageable Target;
        public readonly DamageType DamageType;

        public HitRequest(in IAttacker attacker, in float baseDamage, in IDamageable target, in DamageType damageType)
        {
            this.Attacker = attacker;
            this.BaseDamage = baseDamage;
            this.Target = target;
            this.DamageType = damageType;
        }
    }
}


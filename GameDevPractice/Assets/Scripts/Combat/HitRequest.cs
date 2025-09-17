using UnityEngine;

namespace TH.Combat
{
    public readonly struct HitRequest
    {
        public readonly IAttackable Attacker;
        public readonly float BaseDamage;
        public readonly IDamageable Target;

        public HitRequest(in IAttackable attacker, in float baseDamage, in IDamageable target)
        {
            this.Attacker = attacker;
            this.BaseDamage = baseDamage;
            this.Target = target;
        }
    }
}


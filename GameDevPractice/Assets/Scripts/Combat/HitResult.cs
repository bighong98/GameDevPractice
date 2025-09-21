using UnityEngine;

namespace TH.Combat
{
    public struct HitResult
    {
        public readonly IAttackable Attacker;
        public float Damage;

        public HitResult(in IAttackable attacker)
        {
            Attacker = attacker;
            Damage = default;
        }

        public HitResult(in IAttackable attacker, float damage)
        {
            Attacker = attacker;
            Damage = damage;
        }
    }
}

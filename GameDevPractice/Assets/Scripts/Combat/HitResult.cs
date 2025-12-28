using UnityEngine;

namespace TH.Combat
{
    public struct HitResult
    {
        public readonly IAttacker Attacker;
        public float Damage;

        public HitResult(in IAttacker attacker)
        {
            Attacker = attacker;
            Damage = default;
        }

        public HitResult(in IAttacker attacker, float damage)
        {
            Attacker = attacker;
            Damage = damage;
        }
    }
}

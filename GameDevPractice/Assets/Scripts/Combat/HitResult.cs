using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    public struct HitResult
    {
        public readonly IAttacker Attacker;
        public readonly int AttackInstanceId;
        public readonly IReadOnlyList<float> HitDamages;
        public float Damage;
        public bool HasBatchDamages => HitDamages != null && HitDamages.Count > 0;

        public HitResult(in IAttacker attacker)
        {
            Attacker = attacker;
            AttackInstanceId = 0;
            HitDamages = null;
            Damage = default;
        }

        public HitResult(
            in IAttacker attacker,
            float damage,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null)
        {
            Attacker = attacker;
            AttackInstanceId = attackInstanceId;
            HitDamages = hitDamages;
            Damage = damage;
        }
    }
}

using System.Collections.Generic;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public struct HitResult
    {
        public readonly IAttacker Attacker;
        public readonly int AttackInstanceId;
        public readonly IReadOnlyList<float> HitDamages;
        public readonly SkillTypeSO Skill;
        public readonly Vector3 HitPoint;
        public readonly bool HasHitPoint;
        public float Damage;
        public bool HasBatchDamages => HitDamages != null && HitDamages.Count > 0;

        public HitResult(in IAttacker attacker)
        {
            Attacker = attacker;
            AttackInstanceId = 0;
            HitDamages = null;
            Skill = null;
            HitPoint = default;
            HasHitPoint = false;
            Damage = default;
        }

        public HitResult(
            in IAttacker attacker,
            float damage,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null,
            SkillTypeSO skill = null,
            Vector3 hitPoint = default,
            bool hasHitPoint = false)
        {
            Attacker = attacker;
            AttackInstanceId = attackInstanceId;
            HitDamages = hitDamages;
            Skill = skill;
            HitPoint = hitPoint;
            HasHitPoint = hasHitPoint;
            Damage = damage;
        }
    }
}

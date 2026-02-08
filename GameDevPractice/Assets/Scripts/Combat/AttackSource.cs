using System.Collections.Generic;
using TH.Attribute.Stat;
using UnityEngine;

namespace TH.Combat
{
    public readonly struct AttackSource
    {
        public readonly IAttacker Attacker;
        public readonly IGameStat AttackSourceStat;
        public readonly float BaseDamage;
        public readonly DamageType DamageType;
        public readonly int AttackInstanceId;
        public readonly IReadOnlyList<float> HitDamages;

        public AttackSource(
            IAttacker attacker,
            IGameStat attackSourceStat,
            float baseDamage,
            DamageType damageType,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null)
        {
            Attacker = attacker;
            AttackSourceStat = attackSourceStat;
            BaseDamage = baseDamage;
            DamageType = damageType;
            AttackInstanceId = attackInstanceId;
            HitDamages = hitDamages;
        }

        public AttackSource(IAttacker attacker, IGameStat attackSourceStat, DamageType damageType)
            : this(attacker, attackSourceStat, 0f, damageType, 0, null)
        {
        }

        public AttackSource(IAttacker attacker, float fixedDamage, DamageType damageType)
            : this(attacker, null, fixedDamage, damageType, 0, null)
        {
        }

        public HitRequest ToRequest(IDamageable target)
        {
            return new HitRequest(
                Attacker,
                AttackSourceStat?.Value ?? BaseDamage,
                target,
                DamageType,
                AttackInstanceId,
                HitDamages);
        }
    }

    public enum DamageType
    {
        None,
        Physical,
        Magical,
        TrueDamage,
    }
}

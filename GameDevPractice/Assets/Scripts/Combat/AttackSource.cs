using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Resource;
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
        public readonly SkillTypeSO Skill;
        public readonly IGameSkill RuntimeSkill;
        public readonly string SkillRuntimeId;

        public AttackSource(
            IAttacker attacker,
            IGameStat attackSourceStat,
            float baseDamage,
            DamageType damageType,
            int attackInstanceId = 0,
            IReadOnlyList<float> hitDamages = null,
            SkillTypeSO skill = null,
            IGameSkill runtimeSkill = null,
            string skillRuntimeId = null)
        {
            Attacker = attacker;
            AttackSourceStat = attackSourceStat;
            BaseDamage = baseDamage;
            DamageType = damageType;
            AttackInstanceId = attackInstanceId;
            HitDamages = hitDamages;
            Skill = skill;
            RuntimeSkill = runtimeSkill;
            SkillRuntimeId = !string.IsNullOrWhiteSpace(skillRuntimeId)
                ? skillRuntimeId
                : runtimeSkill?.RuntimeId;
        }

        public AttackSource(IAttacker attacker, IGameStat attackSourceStat, DamageType damageType)
            : this(attacker, attackSourceStat, 0f, damageType, 0, null, null, null, null)
        {
        }

        public AttackSource(IAttacker attacker, float fixedDamage, DamageType damageType)
            : this(attacker, null, fixedDamage, damageType, 0, null, null, null, null)
        {
        }

        public HitRequest ToRequest(IDamageable target, Vector3 hitPoint = default, bool hasHitPoint = false)
        {
            return new HitRequest(
                Attacker,
                AttackSourceStat?.Value ?? BaseDamage,
                target,
                DamageType,
                AttackInstanceId,
                HitDamages,
                Skill,
                RuntimeSkill,
                SkillRuntimeId,
                hitPoint,
                hasHitPoint);
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

using TH.Attribute.Stat;
using UnityEngine;

namespace TH.Combat
{
    public readonly struct AttackSource // 무기, 스킬, 투사체, 장판 등 대미지를 발생시키는 모든 개별 공격의 정보 구조체
    {
        public readonly IAttacker Attacker; // 공격자(AttackSource를 생성한 주체)
        public readonly IGameStat AttackSourceStat; // 공격 대미지 기준 스탯 (AttackSourceStat != null 이면 BaseDamage 무시)
        public readonly float BaseDamage; // 공격 대미지 (치명타, 회피, 방어 등 요소 처리 전)
        public readonly DamageType DamageType;


        public AttackSource(IAttacker attacker, IGameStat attackSourceStat, float baseDamage, DamageType damageType)
        {
            this.Attacker = attacker;
            this.AttackSourceStat = attackSourceStat;
            this.BaseDamage = baseDamage;
            this.DamageType = damageType;
        }

        // 스탯 기반 공격용 생성자
        public AttackSource(IAttacker attacker, IGameStat attackSourceStat, DamageType damageType) 
            : this(attacker, attackSourceStat, 0, damageType) { }

        // 비 스탯 기반 공격용 생성자 (고정 수치 데미지)
        public AttackSource(IAttacker attacker, float fixedDamage, DamageType damageType) 
            : this(attacker, null, fixedDamage, damageType) { }

        public HitRequest ToRequest(IDamageable target)
        {
            return new HitRequest(Attacker, AttackSourceStat?.Value ?? BaseDamage, target, DamageType);
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



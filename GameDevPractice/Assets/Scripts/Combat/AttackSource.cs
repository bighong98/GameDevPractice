using UnityEngine;

namespace TH.Combat
{
    public readonly struct AttackSource // 무기, 스킬, 투사체, 장판 등 대미지를 발생시키는 모든 개별 공격의 정보 구조체
    {
        public readonly IAttackable Attacker; // 공격자(AttackSource를 생성한 주체)
        public readonly float BaseDamage; // 공격 대미지 (치명타, 회피, 방어 등 요소 처리 전)

        public AttackSource(IAttackable attacker, float baseDamage)
        {
            this.Attacker = attacker;
            this.BaseDamage = baseDamage;
        }

        public HitRequest ToRequest(IDamageable target)
        {
            return new HitRequest(Attacker, BaseDamage, target);
        }
    }
}



using UnityEngine;

namespace TH.Combat
{
    public class DamageCalculator : IDamageCalculator
    {
        public HitResult Resolve(in HitRequest hitRequest, in DamageRuleSO damageRule)
        {
            HitResult hitResult = new HitResult(hitRequest.Attacker);
            
            //todo: 대미지 계산 로직 추가
            float damage = hitRequest.BaseDamage;
            hitResult.Damage = damage;
            
            return hitResult;
        }
    }
}


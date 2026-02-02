using UnityEngine;
using TH.Attribute;
using TH.Attribute.Stat;

namespace TH.Combat
{
    public class DamageCalculator : IDamageCalculator
    {
        public HitResult Resolve(in HitRequest hitRequest, in DamageRuleSO damageRule)
        {
            HitResult hitResult = new HitResult(hitRequest.Attacker);
            float currentDamage = hitRequest.BaseDamage;

            // 1. 방어력 계산이 필요한지 검증 (필요한 데이터가 모두 있는지)
            if (ShouldApplyDefense(hitRequest, damageRule, out float defenseValue))
            {
                // 2. 방어력에 따른 대미지 감소 배율 적용
                float multiplier = damageRule.GetMultiplierByDefenseStat(defenseValue);
                currentDamage *= multiplier;
            }

            // 3. 최종 대미지 결정 (음수 방지 및 반올림)
            // 소수점 대미지를 허용하지 않는다면 Round나 Ceil을 사용
            hitResult.Damage = Mathf.Max(0, Mathf.Round(currentDamage)); 

            return hitResult;
        }

        // 방어력 적용 대상인지 확인하고, 적용 대상이라면 방어력 수치를 out으로 반환
        private bool ShouldApplyDefense(in HitRequest request, DamageRuleSO rule, out float defenseVal)
        {
            defenseVal = 0f;

            // 룰이나 타겟이 없으면 방어 계산 불가
            if (rule == null || rule.DefenceStatRuleSO == null) return false;
            if (request.Target is not Component targetComponent) return false;

            // 해당 대미지 타입에 매핑된 방어 스탯이 있는지 확인 (없으면 True Damage 등으로 간주하여 false 반환)
            if (!rule.DefenceStatRuleSO.TryGetValue(request.DamageType, out var defenceStatSO) || defenceStatSO == null)
            {
                return false; 
            }

            // 타겟이 스탯을 가지고 있는지 확인
            if (!targetComponent.TryGetComponent<IStatHolder>(out var statHolder)) return false;
            
            // 실제 스탯 값을 가져올 수 있는지 확인
            if (!statHolder.TryGetStat(defenceStatSO, out var defenceStat)) return false;

            defenseVal = defenceStat.Value;
            return true;
        }
    }
}
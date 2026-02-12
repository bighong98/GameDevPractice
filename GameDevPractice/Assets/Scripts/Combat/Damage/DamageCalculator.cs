using System.Collections.Generic;
using UnityEngine;
using TH.Attribute;
using TH.Attribute.Stat;

namespace TH.Combat
{
    public class DamageCalculator : IDamageCalculator
    {
        public HitResult Resolve(in HitRequest hitRequest, in DamageRuleSO damageRule)
        {
            bool applyDefense = ShouldApplyDefense(hitRequest, damageRule, out float defenseValue);
            float multiplier = applyDefense ? damageRule.GetMultiplierByDefenseStat(defenseValue) : 1f;

            if (hitRequest.HitDamages != null && hitRequest.HitDamages.Count > 0)
            {
                var resolvedHitDamages = new List<float>(hitRequest.HitDamages.Count);
                float totalDamage = 0f;

                for (int i = 0; i < hitRequest.HitDamages.Count; i++)
                {
                    float currentDamage = hitRequest.HitDamages[i];
                    if (applyDefense)
                        currentDamage *= multiplier;

                    float resolvedDamage = Mathf.Max(0f, Mathf.Round(currentDamage));
                    resolvedHitDamages.Add(resolvedDamage);
                    totalDamage += resolvedDamage;
                }

                return new HitResult(
                    hitRequest.Attacker,
                    totalDamage,
                    hitRequest.AttackInstanceId,
                    resolvedHitDamages,
                    hitRequest.Skill,
                    hitRequest.HitPoint,
                    hitRequest.HasHitPoint);
            }

            float singleDamage = hitRequest.BaseDamage;
            if (applyDefense)
                singleDamage *= multiplier;

            singleDamage = Mathf.Max(0f, Mathf.Round(singleDamage));
            return new HitResult(
                hitRequest.Attacker,
                singleDamage,
                hitRequest.AttackInstanceId,
                null,
                hitRequest.Skill,
                hitRequest.HitPoint,
                hitRequest.HasHitPoint);
        }

        private bool ShouldApplyDefense(in HitRequest request, DamageRuleSO rule, out float defenseVal)
        {
            defenseVal = 0f;

            if (rule == null || rule.DefenceStatRuleSO == null)
                return false;
            if (request.Target is not Component targetComponent)
                return false;

            if (!rule.DefenceStatRuleSO.TryGetValue(request.DamageType, out var defenceStatSO) || defenceStatSO == null)
                return false;

            if (!targetComponent.TryGetComponent<IStatHolder>(out var statHolder))
                return false;
            if (!statHolder.TryGetStat(defenceStatSO, out var defenceStat))
                return false;

            defenseVal = defenceStat.Value;
            return true;
        }
    }
}

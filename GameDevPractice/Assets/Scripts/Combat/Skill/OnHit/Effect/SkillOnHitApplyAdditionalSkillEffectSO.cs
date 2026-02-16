using System.Collections.Generic;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillOnHitApplyAdditionalSkillEffectSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/Effect/ApplyAdditionalSkill")]
    public sealed class SkillOnHitApplyAdditionalSkillEffectSO : SkillOnHitEffectSO
    {
        [SerializeField] private SkillTypeSO additionalSkill;
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;
        [SerializeField] private bool reuseAttackInstance = true;
        [SerializeField] private bool includeHitPoint = true;
        [SerializeField] private bool propagateAdditionalSkillReference;

        public SkillTypeSO AdditionalSkill => additionalSkill;
        public float DamageScale => Mathf.Max(0f, damageScale);
        public int HitCountOverride => Mathf.Max(0, hitCountOverride);
        public bool ReuseAttackInstance => reuseAttackInstance;
        public bool IncludeHitPoint => includeHitPoint;
        public bool PropagateAdditionalSkillReference => propagateAdditionalSkillReference;


        public override void Execute(in SkillOnHitProcContext context, ICombatSystem combatSystem)
        {
            if (combatSystem == null || context.Attacker.IsNull() || context.PrimaryTarget.IsNull() || additionalSkill.IsNull())
            {
                return;
            }

            float sourceDamage = ResolveSourceDamage(context.Attacker, additionalSkill);
            float perHitDamage = Mathf.Max(0f, sourceDamage * additionalSkill.AttackCoefficient * Mathf.Max(0f, damageScale));
            if (perHitDamage <= 0f)
            {
                return;
            }

            int hitCount = hitCountOverride > 0 ? hitCountOverride : Mathf.Max(1, additionalSkill.HitCount);
            IReadOnlyList<float> hitDamages = BuildHitDamages(perHitDamage, hitCount);

            bool useHitPoint = includeHitPoint && context.HasHitPosition;
            var request = new HitRequest(
                context.Attacker,
                perHitDamage,
                context.PrimaryTarget,
                additionalSkill.DamageType,
                reuseAttackInstance ? context.AttackInstanceId : 0,
                hitDamages,
                propagateAdditionalSkillReference ? additionalSkill : null,
                useHitPoint ? context.HitPosition : default,
                useHitPoint);

            combatSystem.ApplyHit(request);
        }

        private static float ResolveSourceDamage(IAttacker attacker, SkillTypeSO skill)
        {
            if (attacker is Component attackerComponent &&
                skill.AttackSourceStatSO.IsNotNull() &&
                attackerComponent.TryGetComponent<IStatHolder>(out var statHolder) &&
                statHolder.TryGetStat(skill.AttackSourceStatSO, out var attackSourceStat))
            {
                return attackSourceStat.Value;
            }

            return skill.BaseDamage;
        }

        private static IReadOnlyList<float> BuildHitDamages(float perHitDamage, int hitCount)
        {
            int resolvedHitCount = Mathf.Max(1, hitCount);
            if (resolvedHitCount <= 1)
            {
                return null;
            }

            var hitDamages = new List<float>(resolvedHitCount);
            for (int i = 0; i < resolvedHitCount; i++)
            {
                hitDamages.Add(perHitDamage);
            }

            return hitDamages;
        }
    }
}
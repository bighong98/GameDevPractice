using System;
using System.Threading;
using UnityEngine;
using TH.Resource;
using TH.Utils;
using TH.Attribute;

namespace TH.Combat.Service
{
    public sealed class CombatSystem : ICombatSystem
    {
        private static int _attackSequence;
        public event HitAppliedEvent OnHitApplied;

        private DamageRuleSO damageRule;
        private readonly IDamageCalculator damageCalc;

        public CombatSystem(IResourceLoader resourceLoader, IDamageCalculator damageCalc)
        {
            this.damageCalc = damageCalc;
            resourceLoader.OnLabelResourcesLoadedAll += label =>
            {
                if (!string.Equals(label, Constants.PreLoadLabel))
                    return;
                if (!resourceLoader.TryLoad("DamageRuleSO", out damageRule))
                    Logg.LogError("[CombatSystem] failed to load DamageRuleSO");
            };
        }

        public void ApplyHit(in HitRequest hitRequest)
        {
            var normalizedRequest = NormalizeAttackInstanceId(hitRequest);
            var result = damageCalc.Resolve(normalizedRequest, damageRule);
            if (normalizedRequest.Target is Component { gameObject: { activeSelf: true } })
            {
                Logg.Log($"[{nameof(CombatSystem)}.{nameof(ApplyHit)}]", Logg.LoggingMode.Completed);
                normalizedRequest.Target.TakeDamage(result);
                OnHitApplied?.Invoke(result, normalizedRequest.Target);
            }
        }

        private static HitRequest NormalizeAttackInstanceId(in HitRequest request)
        {
            if (request.AttackInstanceId > 0)
                return request;

            return new HitRequest(
                request.Attacker,
                request.BaseDamage,
                request.Target,
                request.DamageType,
                Interlocked.Increment(ref _attackSequence),
                request.HitDamages,
                request.Skill,
                request.HitPoint,
                request.HasHitPoint);
        }
    }
}

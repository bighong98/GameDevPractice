using System.Collections.Generic;
using TH.Attribute;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillOnHitExplosionAreaDamageEffectSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/Effect/ExplosionAreaDamage")]
    public sealed class SkillOnHitExplosionAreaDamageEffectSO : SkillOnHitEffectSO
    {
        [Header("Area")]
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField, Min(1)] private int maxTargets = 16;
        [SerializeField] private bool includePrimaryTarget = true;
        [SerializeField] private bool onlyPrimaryTargetLayer = true;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float damageScale = 1f;
        [SerializeField, Min(0)] private int hitCountOverride;
        [SerializeField] private bool useSourceSkillDamageType = true;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private bool forwardSourceSkillToSecondaryHits;

        [Header("VFX")]
        [SerializeField] private GameObject explosionVfxPrefab;

        public override void Execute(in SkillOnHitProcContext context, ICombatSystem combatSystem)
        {
            if (combatSystem == null || context.Attacker.IsNull() || context.PrimaryTarget.IsNull())
            {
                return;
            }

            if (radius <= 0f || maxTargets <= 0)
            {
                return;
            }

            var center = context.HasHitPosition ? context.HitPosition : context.PrimaryTarget.transform.position;
            TryPlayExplosionVfx(center);

            int layerMask = onlyPrimaryTargetLayer
                ? 1 << context.PrimaryTarget.gameObject.layer
                : Physics.AllLayers;

            var colliders = Physics.OverlapSphere(
                center,
                radius,
                layerMask,
                QueryTriggerInteraction.Ignore);

            if (colliders == null || colliders.Length == 0)
            {
                return;
            }

            float scaledDamage = Mathf.Max(0f, context.AppliedDamage * Mathf.Max(0f, damageScale));
            if (scaledDamage <= 0f)
            {
                return;
            }

            int resolvedHitCount = ResolveHitCount(context.SourceSkill);
            var resolvedDamageType = ResolveDamageType(context.SourceSkill);
            var resolvedSkillRef = forwardSourceSkillToSecondaryHits ? context.SourceSkill : null;

            int appliedCount = 0;
            var uniqueTargets = new HashSet<Health>();
            for (int i = 0; i < colliders.Length && appliedCount < maxTargets; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.TryGetComponent<Health>(out var target))
                {
                    continue;
                }

                if (!includePrimaryTarget && target == context.PrimaryTarget)
                {
                    continue;
                }

                if (target.IsDead || !uniqueTargets.Add(target))
                {
                    continue;
                }

                var request = new HitRequest(
                    context.Attacker,
                    scaledDamage,
                    target,
                    resolvedDamageType,
                    context.AttackInstanceId,
                    BuildHitDamages(scaledDamage, resolvedHitCount),
                    resolvedSkillRef,
                    center,
                    hasHitPoint: true);

                combatSystem.ApplyHit(request);
                appliedCount++;
            }
        }

        private int ResolveHitCount(SkillTypeSO sourceSkill)
        {
            if (hitCountOverride > 0)
            {
                return hitCountOverride;
            }

            return sourceSkill.IsNotNull()
                ? Mathf.Max(1, sourceSkill.HitCount)
                : 1;
        }

        private DamageType ResolveDamageType(SkillTypeSO sourceSkill)
        {
            if (useSourceSkillDamageType && sourceSkill.IsNotNull())
            {
                return sourceSkill.DamageType;
            }

            return damageType;
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

        private void TryPlayExplosionVfx(Vector3 position)
        {
            if (explosionVfxPrefab == null)
            {
                return;
            }

            PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(explosionVfxPrefab, null, position);
        }
    }
}
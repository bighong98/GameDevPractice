using System;
using System.Collections.Generic;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    [Serializable]
    public sealed class SkillTriggerRuleEntry
    {
        [SerializeField] private bool oncePerAttackInstance = true;
        [SerializeField] private List<SkillConditionSO> conditions = new();
        [SerializeField] private List<SkillTypeSO> triggeredSkills = new();

        public IReadOnlyList<SkillConditionSO> Conditions => conditions;
        public IReadOnlyList<SkillTypeSO> TriggeredSkills => triggeredSkills;
        public bool OncePerAttackInstance => oncePerAttackInstance;
        public bool HasTriggeredSkills => triggeredSkills != null && triggeredSkills.Exists(skill => skill != null);
        public bool HasEffects => HasTriggeredSkills;

#if UNITY_EDITOR
        public void SetTriggeredSkillsForEditor(IReadOnlyList<SkillTypeSO> skills, bool overwrite)
        {
            if (!overwrite && HasTriggeredSkills)
            {
                return;
            }

            triggeredSkills ??= new List<SkillTypeSO>();
            triggeredSkills.Clear();

            if (skills == null || skills.Count == 0)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                triggeredSkills.Add(skill);
            }
        }
#endif

        public SkillTriggerRuleEntry Clone()
        {
            var cloned = new SkillTriggerRuleEntry
            {
                oncePerAttackInstance = oncePerAttackInstance,
                conditions = conditions != null ? new List<SkillConditionSO>(conditions) : new List<SkillConditionSO>(),
                triggeredSkills = CloneTriggeredSkills()
            };
            return cloned;
        }

        public bool Evaluate(in SkillOnHitContext context)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                if (!condition.Evaluate(context))
                {
                    return false;
                }
            }

            return true;
        }

        public void Execute(in SkillOnHitContext context, ICombatSystem combatSystem)
        {
            if (combatSystem == null || triggeredSkills == null)
            {
                Trace("execute skipped. reason=invalid_state");
                return;
            }

            for (int i = 0; i < triggeredSkills.Count; i++)
            {
                var triggeredSkill = triggeredSkills[i];
                if (triggeredSkill == null)
                {
                    Trace($"triggered skill skipped. reason=null_skill, index={i}");
                    continue;
                }

                Trace($"triggered skill execute. index={i}, skill={triggeredSkill.name}, attackId={context.AttackInstanceId}");
                ExecuteTriggeredSkill(triggeredSkill, context, combatSystem);
            }
        }

        private static void ExecuteTriggeredSkill(
            SkillTypeSO skill,
            in SkillOnHitContext context,
            ICombatSystem combatSystem)
        {
            if (combatSystem == null || context.Attacker.IsNull() || context.PrimaryTarget.IsNull() || skill == null)
            {
                Trace($"execute triggered skill skipped. reason=invalid_context, skill={(skill == null ? "null" : skill.name)}");
                return;
            }

            float sourceDamage = ResolveSourceDamage(context.Attacker, skill);
            float perHitDamage = Mathf.Max(0f, sourceDamage * skill.AttackCoefficient);
            if (perHitDamage <= 0f)
            {
                Trace($"execute triggered skill skipped. reason=non_positive_damage, skill={skill.name}, sourceDamage={sourceDamage:0.###}");
                return;
            }

            int hitCount = Mathf.Max(1, skill.HitCount);
            IReadOnlyList<float> hitDamages = BuildHitDamages(perHitDamage, hitCount);

            if (TryExecuteTriggeredSkillByProfile(skill, context, perHitDamage, hitDamages, combatSystem))
            {
                Trace($"execute triggered skill via profile. skill={skill.name}, hitCount={hitCount}");
                return;
            }

            bool useHitPoint = context.HasHitPosition;
            var request = new HitRequest(
                context.Attacker,
                perHitDamage,
                context.PrimaryTarget,
                skill.DamageType,
                context.AttackInstanceId,
                hitDamages,
                skill,
                useHitPoint ? context.HitPosition : default,
                useHitPoint);

            Trace($"execute triggered skill via direct_hit. skill={skill.name}, damage={perHitDamage:0.###}, hitCount={hitCount}");
            combatSystem.ApplyHit(request);
        }

        private static bool TryExecuteTriggeredSkillByProfile(
            SkillTypeSO skill,
            in SkillOnHitContext context,
            float perHitDamage,
            IReadOnlyList<float> hitDamages,
            ICombatSystem combatSystem)
        {
            if (skill == null || skill.ExecutionProfile is not { HasActions: true } executionProfile)
            {
                Trace($"profile execution unavailable. reason=no_profile_actions, skill={(skill == null ? "null" : skill.name)}");
                return false;
            }

            if (context.Attacker is not Component attackerComponent ||
                !TryResolveExecutionEnvironment(attackerComponent, out var primaryExecutionServices, out var coroutineRunner))
            {
                Trace($"profile execution unavailable. reason=no_execution_environment, skill={skill.name}");
                return false;
            }

            var fallbackExecutionServices = new FallbackOnHitExecutionServices(combatSystem);
            ISkillExecutionServices executionServices = primaryExecutionServices != null
                ? new HybridOnHitExecutionServices(primaryExecutionServices, fallbackExecutionServices)
                : fallbackExecutionServices;

            var source = new AttackSource(
                context.Attacker,
                null,
                perHitDamage,
                skill.DamageType,
                context.AttackInstanceId,
                hitDamages,
                skill);

            var executionContext = new SkillExecutionContext(
                context.Attacker,
                context.PrimaryTarget,
                skill,
                source,
                timingScale: 1f);

            Trace($"profile coroutine start. skill={skill.name}, runner={coroutineRunner.GetType().Name}, primaryServices={(primaryExecutionServices == null ? "none" : primaryExecutionServices.GetType().Name)}");
            coroutineRunner.StartCoroutine(executionProfile.Execute(executionContext, executionServices));
            return true;
        }

        private static bool TryResolveExecutionEnvironment(
            Component attackerComponent,
            out ISkillExecutionServices executionServices,
            out MonoBehaviour coroutineRunner)
        {
            executionServices = null;
            coroutineRunner = null;

            if (attackerComponent == null)
            {
                return false;
            }

            if (TryResolveExecutionServicesFromComponent(attackerComponent, out executionServices, out coroutineRunner))
            {
                return true;
            }

            var parentServices = attackerComponent.GetComponentInParent<ISkillExecutionServices>();
            if (parentServices is Component parentComponent &&
                TryResolveExecutionServicesFromComponent(parentComponent, out executionServices, out coroutineRunner))
            {
                return true;
            }

            var childServices = attackerComponent.GetComponentInChildren<ISkillExecutionServices>();
            if (childServices is Component childComponent &&
                TryResolveExecutionServicesFromComponent(childComponent, out executionServices, out coroutineRunner))
            {
                return true;
            }

            if (TryResolveCoroutineRunner(attackerComponent, out coroutineRunner))
            {
                return true;
            }

            Trace("execution environment resolution failed.");
            return false;
        }

        private static bool TryResolveCoroutineRunner(Component source, out MonoBehaviour runner)
        {
            runner = null;

            if (source is MonoBehaviour sourceRunner)
            {
                runner = sourceRunner;
                return true;
            }

            runner = source.GetComponentInParent<MonoBehaviour>();
            if (runner != null)
            {
                return true;
            }

            runner = source.GetComponentInChildren<MonoBehaviour>();
            return runner != null;
        }

        private static bool TryResolveExecutionServicesFromComponent(
            Component source,
            out ISkillExecutionServices executionServices,
            out MonoBehaviour coroutineRunner)
        {
            executionServices = null;
            coroutineRunner = null;

            if (source == null || !source.TryGetComponent<ISkillExecutionServices>(out var services))
            {
                return false;
            }

            if (services is not Component serviceComponent)
            {
                return false;
            }

            executionServices = services;

            if (serviceComponent is MonoBehaviour serviceRunner)
            {
                coroutineRunner = serviceRunner;
                return true;
            }

            if (source is MonoBehaviour sourceRunner)
            {
                coroutineRunner = sourceRunner;
                return true;
            }

            executionServices = null;
            return false;
        }

        private static float ResolveSourceDamage(IAttacker attacker, SkillTypeSO targetSkill)
        {
            if (attacker is Component attackerComponent &&
                targetSkill.AttackSourceStatSO.IsNotNull() &&
                attackerComponent.TryGetComponent<IStatHolder>(out var statHolder) &&
                statHolder.TryGetStat(targetSkill.AttackSourceStatSO, out var attackSourceStat))
            {
                return attackSourceStat.Value;
            }

            return targetSkill.BaseDamage;
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

        private List<SkillTypeSO> CloneTriggeredSkills()
        {
            var cloned = new List<SkillTypeSO>();
            if (triggeredSkills == null || triggeredSkills.Count == 0)
            {
                return cloned;
            }

            for (int i = 0; i < triggeredSkills.Count; i++)
            {
                var skill = triggeredSkills[i];
                if (skill == null)
                {
                    continue;
                }

                cloned.Add(skill);
            }

            return cloned;
        }

        private static void Trace(string msg)
        {
            Logg.Log($"[{nameof(SkillTriggerRuleEntry)}] {msg}", Logg.LoggingMode.InProgress);
        }

        private sealed class FallbackOnHitExecutionServices : ISkillExecutionServices
        {
            private readonly ICombatSystem combatSystem;
            private readonly List<Health> areaTargetsBuffer = new();
            private Collider[] overlapBuffer = new Collider[32];

            public FallbackOnHitExecutionServices(ICombatSystem combatSystem)
            {
                this.combatSystem = combatSystem;
            }

            public bool TryApplyHit(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
            {
                if (combatSystem == null || target.IsNull() || target.IsDead)
                {
                    Trace($"fallback apply hit skipped. reason=invalid_target, target={(target == null ? "null" : target.name)}");
                    return false;
                }

                var source = BuildModifiedAttackSource(context.AttackSource, damageScale, hitCountOverride);
                Trace($"fallback apply hit. skill={(context.Skill == null ? "null" : context.Skill.name)}, target={target.name}, baseDamage={source.BaseDamage:0.###}");
                combatSystem.ApplyHit(source.ToRequest(target));
                return true;
            }

            public bool TryLaunchProjectile(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
            {
                return TryApplyHit(context, target, damageScale, hitCountOverride);
            }

            public IReadOnlyList<Health> FindTargetsInRadius(
                SkillExecutionContext context,
                Vector3 center,
                float radius,
                int maxTargets,
                Health primaryTarget,
                bool includePrimary)
            {
                _ = context;
                areaTargetsBuffer.Clear();

                if (radius <= 0f || maxTargets <= 0)
                {
                    return areaTargetsBuffer;
                }

                int layerMask = primaryTarget.IsNotNull()
                    ? 1 << primaryTarget.gameObject.layer
                    : Physics.AllLayers;

                EnsureOverlapBufferSize(maxTargets);
                int hitCount = Physics.OverlapSphereNonAlloc(
                    center,
                    radius,
                    overlapBuffer,
                    layerMask,
                    QueryTriggerInteraction.Ignore);

                if (includePrimary &&
                    primaryTarget.IsNotNull() &&
                    !primaryTarget.IsDead &&
                    Vector3.Distance(center, primaryTarget.transform.position) <= radius)
                {
                    areaTargetsBuffer.Add(primaryTarget);
                }

                int scanCount = Mathf.Min(hitCount, overlapBuffer.Length);
                for (int i = 0; i < scanCount && areaTargetsBuffer.Count < maxTargets; i++)
                {
                    var collider = overlapBuffer[i];
                    if (collider == null || !collider.TryGetComponent<Health>(out var health))
                    {
                        continue;
                    }

                    if (health.IsDead)
                    {
                        continue;
                    }

                    if (!includePrimary && health == primaryTarget)
                    {
                        continue;
                    }

                    if (areaTargetsBuffer.Contains(health))
                    {
                        continue;
                    }

                    areaTargetsBuffer.Add(health);
                }

                Trace($"fallback find targets. radius={radius:0.###}, hitCount={hitCount}, selected={areaTargetsBuffer.Count}");
                return areaTargetsBuffer;
            }

            private void EnsureOverlapBufferSize(int requiredSize)
            {
                if (requiredSize <= overlapBuffer.Length)
                {
                    return;
                }

                int resized = Mathf.NextPowerOfTwo(requiredSize);
                overlapBuffer = new Collider[Mathf.Max(16, resized)];
            }

            private static AttackSource BuildModifiedAttackSource(
                in AttackSource source,
                float damageScale,
                int hitCountOverride)
            {
                float resolvedScale = Mathf.Max(0f, damageScale);
                int resolvedOverride = Mathf.Max(0, hitCountOverride);
                float sourceBaseDamage = source.AttackSourceStat?.Value ?? source.BaseDamage;

                if (source.HitDamages != null && source.HitDamages.Count > 0)
                {
                    int targetCount = resolvedOverride > 0 ? resolvedOverride : source.HitDamages.Count;
                    var hitDamages = new List<float>(targetCount);

                    for (int i = 0; i < source.HitDamages.Count; i++)
                    {
                        hitDamages.Add(source.HitDamages[i] * resolvedScale);
                    }

                    if (resolvedOverride > 0 && resolvedOverride != hitDamages.Count)
                    {
                        float perHitDamage = hitDamages.Count > 0
                            ? hitDamages[0]
                            : sourceBaseDamage * resolvedScale;

                        hitDamages.Clear();
                        for (int i = 0; i < resolvedOverride; i++)
                        {
                            hitDamages.Add(perHitDamage);
                        }
                    }

                    float firstDamage = hitDamages.Count > 0
                        ? hitDamages[0]
                        : sourceBaseDamage * resolvedScale;

                    return new AttackSource(
                        source.Attacker,
                        null,
                        firstDamage,
                        source.DamageType,
                        source.AttackInstanceId,
                        hitDamages,
                        source.Skill);
                }

                if (resolvedOverride > 1)
                {
                    float perHitDamage = sourceBaseDamage * resolvedScale;
                    var hitDamages = new List<float>(resolvedOverride);
                    for (int i = 0; i < resolvedOverride; i++)
                    {
                        hitDamages.Add(perHitDamage);
                    }

                    return new AttackSource(
                        source.Attacker,
                        null,
                        perHitDamage,
                        source.DamageType,
                        source.AttackInstanceId,
                        hitDamages,
                        source.Skill);
                }

                return new AttackSource(
                    source.Attacker,
                    null,
                    sourceBaseDamage * resolvedScale,
                    source.DamageType,
                    source.AttackInstanceId,
                    null,
                    source.Skill);
            }
        }

        private sealed class HybridOnHitExecutionServices : ISkillExecutionServices
        {
            private readonly ISkillExecutionServices primary;
            private readonly ISkillExecutionServices fallback;

            public HybridOnHitExecutionServices(ISkillExecutionServices primary, ISkillExecutionServices fallback)
            {
                this.primary = primary;
                this.fallback = fallback;
            }

            public bool TryApplyHit(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
            {
                if (primary != null && primary.TryApplyHit(context, target, damageScale, hitCountOverride))
                {
                    Trace($"hybrid apply hit via primary. target={(target == null ? "null" : target.name)}");
                    return true;
                }

                Trace($"hybrid apply hit fallback. target={(target == null ? "null" : target.name)}");
                return fallback != null && fallback.TryApplyHit(context, target, damageScale, hitCountOverride);
            }

            public bool TryLaunchProjectile(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
            {
                if (primary != null && primary.TryLaunchProjectile(context, target, damageScale, hitCountOverride))
                {
                    Trace($"hybrid projectile via primary. target={(target == null ? "null" : target.name)}");
                    return true;
                }

                Trace($"hybrid projectile fallback. target={(target == null ? "null" : target.name)}");
                return fallback != null && fallback.TryLaunchProjectile(context, target, damageScale, hitCountOverride);
            }

            public IReadOnlyList<Health> FindTargetsInRadius(
                SkillExecutionContext context,
                Vector3 center,
                float radius,
                int maxTargets,
                Health primaryTarget,
                bool includePrimary)
            {
                if (primary != null)
                {
                    var primaryTargets = primary.FindTargetsInRadius(
                        context,
                        center,
                        radius,
                        maxTargets,
                        primaryTarget,
                        includePrimary);

                    if (primaryTargets != null && primaryTargets.Count > 0)
                    {
                        Trace($"hybrid target search via primary. count={primaryTargets.Count}");
                        return primaryTargets;
                    }
                }

                Trace("hybrid target search fallback.");
                return fallback != null
                    ? fallback.FindTargetsInRadius(context, center, radius, maxTargets, primaryTarget, includePrimary)
                    : Array.Empty<Health>();
            }
        }
    }
}

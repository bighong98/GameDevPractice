using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    [Serializable]
    public sealed class GameSkill : IGameSkill
    {
        [SerializeField] private SkillTypeSO definition;
        [SerializeField] private string runtimeId;
        private readonly Func<SkillTypeSO, IGameSkill> runtimeSkillResolver;
        private IReadOnlyList<IGameSkill> subSkills;


        private bool hasCooldownOverride;
        private float cooldownOverride;
        private bool hasRangeOverride;
        private float rangeOverride;

        public GameSkill(SkillTypeSO definition,
            Func<SkillTypeSO, IGameSkill> runtimeSkillResolver = null,
            string runtimeId = null)
        {
            this.definition = definition;
            this.runtimeSkillResolver = runtimeSkillResolver;

            this.runtimeId = string.IsNullOrWhiteSpace(runtimeId)
                ? Guid.NewGuid().ToString("N")
                : runtimeId;
        }

        public string RuntimeId => runtimeId;
        public SkillTypeSO Definition => definition;
        public string Name => definition != null ? definition.name : string.Empty;
        public string SkillId => definition != null ? definition.SkillId : string.Empty;

        public GameStatSO AttackSourceStatSO => definition != null ? definition.AttackSourceStatSO : null;
        public float BaseDamage => definition != null ? definition.BaseDamage : 0f;
        public int HitCount => definition != null ? definition.HitCount : 1;
        public float AttackCoefficient => definition != null ? definition.AttackCoefficient : 0f;
        public DamageType DamageType => definition != null ? definition.DamageType : DamageType.None;

        public float Range => hasRangeOverride
            ? Mathf.Max(0f, rangeOverride)
            : (definition != null ? definition.Range : 0f);

        public float Cooldown => hasCooldownOverride
            ? Mathf.Max(0f, cooldownOverride)
            : (definition != null ? definition.Cooldown : 0f);

        public bool HasCooldownOverride => hasCooldownOverride;
        public bool HasRangeOverride => hasRangeOverride;

        public float ComboTimeout => definition != null ? definition.ComboTimeout : 0f;
        public int ComboStepCount => definition != null ? definition.ComboStepCount : 0;
        public bool HasComboSteps => definition != null && definition.HasComboSteps;
        public SkillTypeSO.ActiveSkillPostExecutionPolicy PostExecutionPolicy => definition != null
            ? definition.PostExecutionPolicy
            : SkillTypeSO.ActiveSkillPostExecutionPolicy.KeepActive;
        public bool PreserveStepWithInterfere => definition != null && definition.PreserveStepWithInterfere;

        public SkillExecutionProfileSO ExecutionProfile => definition != null ? definition.ExecutionProfile : null;
        public IReadOnlyList<SkillTriggerRuleEntry> OnHitProcEntries => definition != null ? definition.OnHitProcEntries : null;
        public bool HasOnHitProcEntries => definition != null && definition.HasOnHitProcEntries;

        public bool HasSubSkills => definition != null && definition.HasSubSkills;
        public IReadOnlyList<IGameSkill> SubSkills => ResolveSubSkills();

        public SkillTargetPolicy TargetPolicy => definition != null ? definition.TargetPolicy : SkillTargetPolicy.EnemyOnlyDefault;
        public float AnimationSpeedMultiplier => definition != null ? definition.AnimationSpeedMultiplier : 1f;
        public bool AffectedByAttackSpeed => definition != null && definition.AffectedByAttackSpeed;

        public SkillCategory SkillCategory => definition != null ? definition.SkillCategory : SkillCategory.AdditiveSkill;
        public Sprite SkillSlotImage => definition != null ? definition.SkillSlotImage : null;
        public AnimatorOverrideController AnimatorOverride => definition != null ? definition.AnimatorOverride : null;

        public bool HasProjectile => definition != null && definition.HasProjectile;
        public GameObject ProjectilePrefab => definition != null ? definition.ProjectilePrefab : null;
        public bool ProjectilePierceTargets => definition != null && definition.ProjectilePierceTargets;
        public int ProjectileMaxPierceTargets => definition != null ? definition.ProjectileMaxPierceTargets : 0;
        public float ProjectileMaxTravelDistance => definition != null ? definition.ProjectileMaxTravelDistance : 0f;

        public IReadOnlyList<SkillEffectCue> SkillVfxCues => definition != null ? definition.SkillVfxCues : null;
        public bool HasSkillVfxCues => definition != null && definition.HasSkillVfxCues;

        public IGameSkill GetComboStepSkill(int index, IGameSkill fallback)
        {
            if (definition == null)
            {
                return fallback;
            }

            var fallbackDefinition = fallback?.Definition;
            var comboStepDefinition = definition.GetComboStepSkill(index, fallbackDefinition);
            var resolved = ResolveSkillFromDefinition(comboStepDefinition);
            return resolved ?? fallback;
        }

        private IGameSkill ResolveSkillFromDefinition(SkillTypeSO skillDefinition)
        {
            if (skillDefinition == null)
            {
                return null;
            }

            return runtimeSkillResolver?.Invoke(skillDefinition) ?? new GameSkill(skillDefinition);
        }

        private IReadOnlyList<IGameSkill> ResolveSubSkills()
        {
            if (definition == null || !definition.HasSubSkills)
            {
                return Array.Empty<IGameSkill>();
            }

            var source = definition.SubSkills;
            if (source == null || source.Count == 0)
            {
                return Array.Empty<IGameSkill>();
            }

            if (subSkills is List<IGameSkill> cached && cached.Count == source.Count)
            {
                return subSkills;
            }

            var resolved = new List<IGameSkill>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var resolvedSkill = ResolveSkillFromDefinition(source[i]);
                if (resolvedSkill != null)
                {
                    resolved.Add(resolvedSkill);
                }
            }

            subSkills = resolved;
            return subSkills;
        }

        public void SetCooldownOverride(float cooldown)
        {
            hasCooldownOverride = true;
            cooldownOverride = Mathf.Max(0f, cooldown);
        }

        public void ClearCooldownOverride()
        {
            hasCooldownOverride = false;
            cooldownOverride = 0f;
        }

        public void SetRangeOverride(float range)
        {
            hasRangeOverride = true;
            rangeOverride = Mathf.Max(0f, range);
        }

        public void ClearRangeOverride()
        {
            hasRangeOverride = false;
            rangeOverride = 0f;
        }

        public override string ToString()
        {
            return $"{Name}#{RuntimeId}";
        }
    }
}

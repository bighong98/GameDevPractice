using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public interface IGameSkill
    {
        string RuntimeId { get; }
        SkillTypeSO Definition { get; }
        string Name { get; }
        string SkillId { get; }

        GameStatSO AttackSourceStatSO { get; }
        float BaseDamage { get; }
        int HitCount { get; }
        float AttackCoefficient { get; }
        DamageType DamageType { get; }

        float Range { get; }
        float Cooldown { get; }
        bool HasCooldownOverride { get; }
        bool HasRangeOverride { get; }

        float ComboTimeout { get; }
        int ComboStepCount { get; }
        bool HasComboSteps { get; }
        SkillTypeSO.ActiveSkillPostExecutionPolicy PostExecutionPolicy { get; }
        bool PreserveStepWithInterfere { get; }

        SkillExecutionProfileSO ExecutionProfile { get; }
        IReadOnlyList<SkillTriggerRuleEntry> OnHitProcEntries { get; }
        bool HasOnHitProcEntries { get; }

        bool HasSubSkills { get; }
        IReadOnlyList<IGameSkill> SubSkills { get; }

        SkillTargetPolicy TargetPolicy { get; }
        float AnimationSpeedMultiplier { get; }
        bool AffectedByAttackSpeed { get; }

        SkillCategory SkillCategory { get; }
        Sprite SkillSlotImage { get; }
        AnimatorOverrideController AnimatorOverride { get; }

        bool HasProjectile { get; }
        GameObject ProjectilePrefab { get; }
        bool ProjectilePierceTargets { get; }
        int ProjectileMaxPierceTargets { get; }
        float ProjectileMaxTravelDistance { get; }

        IReadOnlyList<SkillEffectCue> SkillVfxCues { get; }
        bool HasSkillVfxCues { get; }

        IGameSkill GetComboStepSkill(int index, IGameSkill fallback);
        void SetCooldownOverride(float cooldown);
        void ClearCooldownOverride();
        void SetRangeOverride(float range);
        void ClearRangeOverride();
    }
}

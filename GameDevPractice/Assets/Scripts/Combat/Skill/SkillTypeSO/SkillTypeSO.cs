using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Combat;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SkillTypeSO", menuName = "Scriptable Objects/Type/Skill/SkillTypeSO")]
public partial class SkillTypeSO : ScriptableObject, IAsyncInitializer
{
    private const float DefaultComboTimeout = 0.75f;

    public enum ActiveSkillPostExecutionPolicy
    {
        KeepActive = 0,
        DeactivateAndResetCombo = 1,
        DeactivateAndKeepCombo = 2
    }

    [Header("Skill Data")]
    [SerializeField] private string skillId;
    [SerializeField] private GameStatSO attackSourceStatSO;
    [SerializeField] private float baseDamage = 1f;
    [SerializeField, Min(1)] private int hitCount = 1;
    [SerializeField, Min(0f)] private float attackCoefficient = 1f;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float range = 2f;
    [SerializeField, Min(0f)] private float cooldown = 1f;

    [SerializeField] private SkillExecutionProfileSO executionProfile;

    [Header("Sub Skill")]
    [SerializeField] private List<SkillTypeSO> subSkills = new();

    [Header("OnHit Triggered Skill")]
    [SerializeField] private List<SkillTriggerRuleEntry> onHitTriggeredSkills = new();

#if UNITY_EDITOR
    [Header("OnHit Triggered Skill Preset(Editor Only)")]
    [SerializeField] private List<SkillOnHitEffectSO> onHitEffects = new();
    [SerializeField, HideInInspector] private int lastImportedOnHitEffectsSignature;

#endif
    [Header("Post Execution")]
    [SerializeField] private ActiveSkillPostExecutionPolicy activeSkillPostExecutionPolicy = ActiveSkillPostExecutionPolicy.KeepActive;

    [Header("Sequence")]
    [SerializeField] private bool preserveStepWithInterfere;
    [SerializeField, Min(0f)] private float comboTimeout = DefaultComboTimeout;
    [SerializeField] private List<SkillTypeSO> comboSteps = new();

#if UNITY_EDITOR
    [Header("Sequence Preset (Editor Only)")]
    [SerializeField] private ComboSequenceSO comboSequence;
    [SerializeField, HideInInspector] private ComboSequenceSO lastImportedComboSequence;
#endif

    [Header("Targeting")]
    [SerializeField] private SkillTargetPolicy targetPolicy = SkillTargetPolicy.EnemyOnlyDefault;

    [Header("Animation Speed")]
    [SerializeField, Min(0.01f)] private float animationSpeedMultiplier = 1f;
    [SerializeField] private bool affectedByAttackSpeed = true;

    [Header("Animation")]
    [NonSerialized] private AnimatorOverrideController animatorOverride;
    [SerializeField] private AssetReferenceAnimatorOverrideController animatorOverrideReference;

    [Header("Projectile")]
    [NonSerialized] private GameObject projectilePrefab;
    [SerializeField] private AssetReferenceGameObject projectilePrefabReference;
    [SerializeField] private bool projectilePierceTargets;
    [SerializeField, Min(0)] private int projectileMaxPierceTargets;
    [SerializeField, Min(0f)] private float projectileMaxTravelDistance;

    [Header("VFX")]
    [SerializeField] private List<SkillEffectCue> skillVfxCues = new();
#if UNITY_EDITOR
    [Header("VFX Preset (Editor Only)")]
    [SerializeField] private SkillEffectProfileSO skillEffectProfile;
    [SerializeField, HideInInspector] private int lastImportedSkillVfxProfileSignature;
#endif


    [Header("UI")]
    [SerializeField] private SkillCategory skillCategory = SkillCategory.AdditiveSkill;
    [SerializeField] private AssetReferenceSprite skillSlotImageReference;
    [NonSerialized] private Sprite skillSlotImage;

    [NonSerialized] private bool initialized;

    public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
    public GameStatSO AttackSourceStatSO => attackSourceStatSO;
    public float BaseDamage => baseDamage;
    public int HitCount => Mathf.Max(1, hitCount);
    public float AttackCoefficient => Mathf.Max(0f, attackCoefficient);
    public DamageType DamageType => damageType;
    public float Range => range;
    public float Cooldown => cooldown;
    public float ComboTimeout => Mathf.Max(0f, comboTimeout);
    public int ComboStepCount => comboSteps?.Count ?? 0;
    public bool HasComboSteps => comboSteps != null && comboSteps.Exists(skill => skill != null);
    public IReadOnlyList<SkillTypeSO> ComboSteps => comboSteps;
    public ActiveSkillPostExecutionPolicy PostExecutionPolicy => activeSkillPostExecutionPolicy;
    public bool PreserveStepWithInterfere => preserveStepWithInterfere;
    public SkillExecutionProfileSO ExecutionProfile => executionProfile;
    public IReadOnlyList<SkillTriggerRuleEntry> OnHitProcEntries => onHitTriggeredSkills;
    public bool HasOnHitProcEntries => onHitTriggeredSkills != null && onHitTriggeredSkills.Exists(entry => entry != null && entry.HasTriggeredSkills);
    public bool HasSubSkills => subSkills != null && subSkills.Exists(skill => skill != null);
    public IReadOnlyList<SkillTypeSO> SubSkills => subSkills;
    public SkillTargetPolicy TargetPolicy => targetPolicy;
    public float AnimationSpeedMultiplier => Mathf.Max(0.01f, animationSpeedMultiplier);
    public bool AffectedByAttackSpeed => affectedByAttackSpeed;
    public SkillCategory SkillCategory => skillCategory;
    public Sprite SkillSlotImage => skillSlotImage;
    public AnimatorOverrideController AnimatorOverride => animatorOverride;
    public bool HasProjectile => ProjectilePrefab != null;
    public GameObject ProjectilePrefab => projectilePrefab;
    public bool ProjectilePierceTargets => projectilePierceTargets;
    public int ProjectileMaxPierceTargets => Mathf.Max(0, projectileMaxPierceTargets);
    public float ProjectileMaxTravelDistance => Mathf.Max(0f, projectileMaxTravelDistance);
    public IReadOnlyList<SkillEffectCue> SkillVfxCues => skillVfxCues;
    public bool HasSkillVfxCues => skillVfxCues != null && skillVfxCues.Exists(cue => cue != null && cue.IsValid);
#if UNITY_EDITOR
    public SkillEffectProfileSO SkillEffectProfile => skillEffectProfile;
#endif

    public async UniTask InitializeAsync(CancellationToken token)
    {
        if (initialized)
        {
            return;
        }

        if (animatorOverrideReference != null && animatorOverrideReference.RuntimeKeyIsValid())
        {
            var loadedAnimatorOverride = await ResourceManager.Instance.ExtractAssetRefAsync<AnimatorOverrideController>(animatorOverrideReference, token);
            if (loadedAnimatorOverride != null)
            {
                animatorOverride = loadedAnimatorOverride;
            }
        }

        if (projectilePrefabReference != null && projectilePrefabReference.RuntimeKeyIsValid())
        {
            var loadedProjectilePrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(projectilePrefabReference, token);
            if (loadedProjectilePrefab != null)
            {
                projectilePrefab = loadedProjectilePrefab;
            }
        }

        await EnsureSkillSlotImageLoadedAsync(token);

        if (comboSteps != null)
        {
            for (int i = 0; i < comboSteps.Count; i++)
            {
                var comboStep = comboSteps[i];
                if (comboStep == null || comboStep == this)
                {
                    continue;
                }

                await comboStep.EnsureSkillSlotImageLoadedAsync(token);
            }
        }

        if (skillVfxCues != null)
        {
            for (int i = 0; i < skillVfxCues.Count; i++)
            {
                var cue = skillVfxCues[i];
                if (cue == null)
                {
                    continue;
                }

                await cue.InitializeAsync(token);
            }
        }

        initialized = true;
    }

    public SkillTypeSO GetComboStepSkill(int index, SkillTypeSO fallback)
    {
        if (!HasComboSteps)
        {
            return fallback;
        }

        if (index < 0 || index >= comboSteps.Count)
        {
            return fallback;
        }

        return comboSteps[index] != null ? comboSteps[index] : fallback;
    }


    private async UniTask EnsureSkillSlotImageLoadedAsync(CancellationToken token)
    {
        if (skillSlotImage != null)
        {
            return;
        }

        if (skillSlotImageReference == null || !skillSlotImageReference.RuntimeKeyIsValid())
        {
            return;
        }

        var loadedSkillSlotImage = await ResourceManager.Instance.ExtractAssetRefAsync<Sprite>(skillSlotImageReference, token);
        if (loadedSkillSlotImage != null)
        {
            skillSlotImage = loadedSkillSlotImage;
        }
    }
}
}

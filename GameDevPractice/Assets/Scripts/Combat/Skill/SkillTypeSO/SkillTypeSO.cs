using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Combat;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SkillTypeSO", menuName = "Scriptable Objects/Type/Skill/SkillTypeSO")]
    public class SkillTypeSO : ScriptableObject
    {
        private const float DefaultComboTimeout = 0.75f;

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

        [Header("OnHit Effects")]
        [SerializeField] private List<SkillTriggerRuleEntry> onHitTriggeredSkills = new();

#if UNITY_EDITOR
        [Header("OnHit Effects Preset(Editor Only)")]
        [SerializeField] private List<SkillOnHitEffectSO> onHitEffects = new();
        [SerializeField, HideInInspector] private int lastImportedOnHitEffectsSignature;

#endif
        [Header("Sequence")]
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
        [SerializeField] private AnimatorOverrideController animatorOverride;

        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab;

        [Header("VFX")]
        [SerializeField] private GameObject skillVFXPrefab;
        [SerializeField] private GameObject onHitVFXPrefab;

        [Header("SFX")]
        [SerializeField] private AudioClip castSfx;

        [Header("UI")]
        [SerializeField] private SkillCategory skillCategory = SkillCategory.AdditiveSkill;
        [SerializeField] private Sprite skillSlotImage;

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
        public SkillExecutionProfileSO ExecutionProfile => executionProfile;
        public IReadOnlyList<SkillTriggerRuleEntry> OnHitProcEntries => onHitTriggeredSkills;
        public bool HasOnHitProcEntries => onHitTriggeredSkills != null && onHitTriggeredSkills.Exists(entry => entry != null && entry.HasTriggeredSkills);
        public bool HasSubSkills => subSkills != null && subSkills.Exists(skill => skill != null);
        public IReadOnlyList<SkillTypeSO> SubSkills => subSkills;
        public SkillTargetPolicy TargetPolicy => targetPolicy;
        public float AnimationSpeedMultiplier => Mathf.Max(0.01f, animationSpeedMultiplier);
        public bool AffectedByAttackSpeed => affectedByAttackSpeed;
        public AudioClip CastSFX => castSfx;
        public SkillCategory SkillCategory => skillCategory;
        public Sprite SkillSlotImage => skillSlotImage;
        public AnimatorOverrideController AnimatorOverride => animatorOverride;
        public bool HasProjectile => projectilePrefab != null;
        public GameObject ProjectilePrefab => projectilePrefab;
        public bool HasSkillEffect => skillVFXPrefab != null;
        public GameObject SkillEffectPrefab => skillVFXPrefab;
        public bool HasOnHitEffect => onHitVFXPrefab != null;
        public GameObject OnHitEffectPrefab => onHitVFXPrefab;
        public bool HasImpactEffect => HasOnHitEffect;
        public GameObject ImpactParticlePrefab => onHitVFXPrefab;

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

#if UNITY_EDITOR
        public ComboSequenceSO ComboSequence => comboSequence;

        private void OnValidate()
        {
            TryAutoImportComboSequence();
            TryAutoImportOnHitEffects();
        }

        private void TryAutoImportComboSequence()
        {
            bool changed = false;

            if (comboSequence == null)
            {
                if (lastImportedComboSequence != null)
                {
                    lastImportedComboSequence = null;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            if (comboSequence == lastImportedComboSequence)
            {
                return;
            }

            if (IsInlineComboAtDefault())
            {
                ImportComboSequence(comboSequence, overwrite: true);
                changed = true;
            }

            lastImportedComboSequence = comboSequence;
            changed = true;

            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
        }

        private bool IsInlineComboAtDefault()
        {
            bool hasConfiguredSteps = comboSteps != null && comboSteps.Count > 0;
            return !hasConfiguredSteps && Mathf.Approximately(comboTimeout, DefaultComboTimeout);
        }

        private void ImportComboSequence(ComboSequenceSO source, bool overwrite)
        {
            if (source == null)
            {
                return;
            }

            if (!overwrite && !IsInlineComboAtDefault())
            {
                return;
            }

            comboTimeout = source.ComboTimeout;
            comboSteps ??= new List<SkillTypeSO>();
            comboSteps.Clear();

            var sourceSteps = source.ComboSteps;
            if (sourceSteps == null)
            {
                return;
            }

            for (int i = 0; i < sourceSteps.Count; i++)
            {
                comboSteps.Add(sourceSteps[i]);
            }
        }

        [ContextMenu("Reimport Combo Sequence Preset (Force)")]
        private void ReimportComboSequencePresetInEditor()
        {
            if (comboSequence == null)
            {
                Debug.LogWarning($"[SkillTypeSO:{name}] comboSequence preset is null.", this);
                return;
            }

            ImportComboSequence(comboSequence, overwrite: true);
            lastImportedComboSequence = comboSequence;
            EditorUtility.SetDirty(this);
        }

        private void TryAutoImportOnHitEffects()
        {
            bool changed = false;

            if (onHitEffects == null || onHitEffects.Count == 0)
            {
                if (lastImportedOnHitEffectsSignature != 0)
                {
                    lastImportedOnHitEffectsSignature = 0;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            int signature = ComputeOnHitEffectsSignature();
            if (signature == lastImportedOnHitEffectsSignature)
            {
                return;
            }

            bool imported = ImportOnHitEffects(out int skippedCount);
            if (imported)
            {
                changed = true;
            }

            if (skippedCount > 0)
            {
                Debug.LogWarning($"[SkillTypeSO:{name}] onHitEffects skipped {skippedCount} unsupported effect(s) during auto import.", this);
            }

            lastImportedOnHitEffectsSignature = signature;
            changed = true;

            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
        }

        private bool ImportOnHitEffects(out int skippedCount)
        {
            skippedCount = 0;

            onHitTriggeredSkills ??= new List<SkillTriggerRuleEntry>();
            onHitTriggeredSkills.Clear();

            if (onHitEffects == null || onHitEffects.Count == 0)
            {
                return true;
            }

            var convertedSkills = new List<SkillTypeSO>(onHitEffects.Count);
            for (int i = 0; i < onHitEffects.Count; i++)
            {
                if (TryConvertOnHitEffectToTriggeredSkill(onHitEffects[i], out var convertedSkill))
                {
                    convertedSkills.Add(convertedSkill);
                }
                else
                {
                    skippedCount++;
                }
            }

            if (convertedSkills.Count == 0)
            {
                return true;
            }

            var entry = new SkillTriggerRuleEntry();
            entry.SetTriggeredSkillsForEditor(convertedSkills, overwrite: true);
            onHitTriggeredSkills.Add(entry);
            return true;
        }

        [ContextMenu("Reimport OnHit Effects (Force)")]
        private void ReimportOnHitEffectsInEditor()
        {
            if (onHitEffects == null || onHitEffects.Count == 0)
            {
                Debug.LogWarning($"[SkillTypeSO:{name}] onHitEffects is empty.", this);
                return;
            }

            _ = ImportOnHitEffects(out int skippedCount);
            lastImportedOnHitEffectsSignature = ComputeOnHitEffectsSignature();

            if (skippedCount > 0)
            {
                Debug.LogWarning($"[SkillTypeSO:{name}] onHitEffects skipped {skippedCount} unsupported effect(s) during force reimport.", this);
            }

            EditorUtility.SetDirty(this);
        }

        private static bool TryConvertOnHitEffectToTriggeredSkill(SkillOnHitEffectSO effect, out SkillTypeSO convertedSkill)
        {
            convertedSkill = null;

            if (effect is not SkillOnHitApplyAdditionalSkillEffectSO additional)
            {
                return false;
            }

            if (additional.AdditionalSkill == null)
            {
                return false;
            }

            convertedSkill = additional.AdditionalSkill;
            return true;
        }

        private int ComputeOnHitEffectsSignature()
        {
            if (onHitEffects == null || onHitEffects.Count == 0)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + onHitEffects.Count;

                for (int i = 0; i < onHitEffects.Count; i++)
                {
                    int id = onHitEffects[i] != null ? onHitEffects[i].GetInstanceID() : 0;
                    hash = (hash * 31) + id;
                }

                return hash;
            }
        }



        [ContextMenu("Validate Skill (Editor)")]
        private void ValidateSkillInEditor()
        {
            ValidateAndLogInEditor();
        }

        public bool ValidateAndLogInEditor(string logPrefix = null)
        {
            var isValid = ValidateInEditor(out var errors, out var warnings);
            var prefix = string.IsNullOrWhiteSpace(logPrefix)
                ? $"[SkillTypeSO:{name}]"
                : $"[{logPrefix}][SkillTypeSO:{name}]";

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log($"{prefix} Validation passed.", this);
                return true;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning($"{prefix} Warning: {warnings[i]}", this);
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"{prefix} Error: {errors[i]}", this);
            }

            return isValid;
        }

        public bool ValidateInEditor(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(skillId))
            {
                warnings.Add("skillId is empty. Asset name fallback will be used at runtime.");
            }

            if (float.IsNaN(baseDamage) || float.IsInfinity(baseDamage))
            {
                errors.Add("baseDamage must be a finite number.");
            }

            if (hitCount < 1)
            {
                errors.Add("hitCount must be >= 1.");
            }

            if (float.IsNaN(attackCoefficient) || float.IsInfinity(attackCoefficient) || attackCoefficient < 0f)
            {
                errors.Add("attackCoefficient must be a finite value >= 0.");
            }

            if (float.IsNaN(range) || float.IsInfinity(range) || range < 0f)
            {
                errors.Add("range must be a finite value >= 0.");
            }

            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                errors.Add("cooldown must be a finite value >= 0.");
            }

            if (float.IsNaN(comboTimeout) || float.IsInfinity(comboTimeout) || comboTimeout < 0f)
            {
                errors.Add("comboTimeout must be a finite value >= 0.");
            }

            if (attackSourceStatSO == null && baseDamage <= 0f)
            {
                warnings.Add("attackSourceStatSO is null and baseDamage <= 0. Actual damage may become 0.");
            }

            if (attackCoefficient <= 0f)
            {
                warnings.Add("attackCoefficient is 0. Actual damage may become 0.");
            }

            if (executionProfile != null && !executionProfile.HasActions)
            {
                errors.Add("executionProfile is assigned but has no actions.");
            }

            ValidateInlineOnHitProc(errors, warnings);
            ValidateInlineCombo(errors, warnings);
            ValidateSubSkills(errors);
            ValidateEffectPrefab(skillVFXPrefab, nameof(skillVFXPrefab), warnings);
            ValidateEffectPrefab(onHitVFXPrefab, nameof(onHitVFXPrefab), warnings);
            ValidateAnimatorOverride(errors, warnings);
            return errors.Count == 0;
        }

        private void ValidateInlineOnHitProc(List<string> errors, List<string> warnings)
        {
            if (onHitTriggeredSkills == null || onHitTriggeredSkills.Count == 0)
            {
                return;
            }

            bool hasValidEntry = false;
            for (int i = 0; i < onHitTriggeredSkills.Count; i++)
            {
                var entry = onHitTriggeredSkills[i];
                if (entry == null)
                {
                    errors.Add($"onHitProcEntries[{i}] is null.");
                    continue;
                }

                var triggered = entry.TriggeredSkills;
                if (triggered == null || triggered.Count == 0)
                {
                    warnings.Add($"onHitProcEntries[{i}] has no triggered skills.");
                    continue;
                }

                bool hasValidTriggeredSkill = false;
                for (int triggerIndex = 0; triggerIndex < triggered.Count; triggerIndex++)
                {
                    var triggerSkill = triggered[triggerIndex];
                    if (triggerSkill == null)
                    {
                        warnings.Add($"onHitProcEntries[{i}].triggeredSkills[{triggerIndex}] is null.");
                        continue;
                    }

                    hasValidTriggeredSkill = true;
                    if (triggerSkill == this)
                    {
                        errors.Add($"onHitProcEntries[{i}].triggeredSkills[{triggerIndex}] references self skill.");
                    }
                }

                if (!hasValidTriggeredSkill)
                {
                    warnings.Add($"onHitProcEntries[{i}] contains no valid triggered skills.");
                    continue;
                }

                hasValidEntry = true;
            }

            if (!hasValidEntry)
            {
                errors.Add("onHitProcEntries contains no valid entries with triggered skills.");
                return;
            }

            if (HasOnHitProcCycle(this, new HashSet<SkillTypeSO>(), new HashSet<SkillTypeSO>()))
            {
                errors.Add("onHitProcEntries contains a cyclic triggered-skill reference.");
            }
        }

        private static bool HasOnHitProcCycle(
            SkillTypeSO node,
            HashSet<SkillTypeSO> visiting,
            HashSet<SkillTypeSO> visited)
        {
            if (node == null)
            {
                return false;
            }

            if (visiting.Contains(node))
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            visiting.Add(node);

            if (node.onHitTriggeredSkills != null)
            {
                for (int entryIndex = 0; entryIndex < node.onHitTriggeredSkills.Count; entryIndex++)
                {
                    var entry = node.onHitTriggeredSkills[entryIndex];
                    if (entry == null || !entry.HasTriggeredSkills)
                    {
                        continue;
                    }

                    var triggered = entry.TriggeredSkills;
                    for (int triggerIndex = 0; triggerIndex < triggered.Count; triggerIndex++)
                    {
                        var triggerSkill = triggered[triggerIndex];
                        if (triggerSkill == null)
                        {
                            continue;
                        }

                        if (HasOnHitProcCycle(triggerSkill, visiting, visited))
                        {
                            return true;
                        }
                    }
                }
            }

            visiting.Remove(node);
            return false;
        }

        private void ValidateInlineCombo(List<string> errors, List<string> warnings)
        {
            if (comboSteps == null || comboSteps.Count == 0)
            {
                return;
            }

            bool hasValidStep = false;
            var duplicateCheck = new HashSet<SkillTypeSO>();

            for (int i = 0; i < comboSteps.Count; i++)
            {
                var step = comboSteps[i];
                if (step == null)
                {
                    errors.Add($"comboSteps[{i}] is null.");
                    continue;
                }

                hasValidStep = true;

                if (!duplicateCheck.Add(step))
                {
                    warnings.Add($"Duplicate combo step reference detected. index={i}, skill={step.name}");
                }

                if (step == this)
                {
                    errors.Add($"comboSteps[{i}] references self.");
                }

                if (step.HasComboSteps)
                {
                    warnings.Add($"Step skill has its own inline combo steps. index={i}, skill={step.name}");
                }

                if (step.AnimatorOverride == null)
                {
                    warnings.Add($"Step skill has no animator override. index={i}, skill={step.name}");
                }
            }

            if (!hasValidStep)
            {
                errors.Add("comboSteps contains no valid step skills.");
                return;
            }

            if (HasComboCycle(this, new HashSet<SkillTypeSO>(), new HashSet<SkillTypeSO>()))
            {
                errors.Add("comboSteps contains a cyclic reference.");
            }
        }

        private static bool HasComboCycle(
            SkillTypeSO node,
            HashSet<SkillTypeSO> visiting,
            HashSet<SkillTypeSO> visited)
        {
            if (node == null)
            {
                return false;
            }

            if (visiting.Contains(node))
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            visiting.Add(node);

            if (node.comboSteps != null)
            {
                for (int i = 0; i < node.comboSteps.Count; i++)
                {
                    if (HasComboCycle(node.comboSteps[i], visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(node);
            return false;
        }

        private void ValidateSubSkills(List<string> errors)
        {
            if (subSkills == null || subSkills.Count == 0)
            {
                return;
            }

            bool hasSelfReference = false;
            for (int i = 0; i < subSkills.Count; i++)
            {
                if (subSkills[i] == this)
                {
                    errors.Add("subSkills contains self reference.");
                    hasSelfReference = true;
                    break;
                }
            }

            if (hasSelfReference)
            {
                return;
            }

            if (HasSubSkillCycle(this, new HashSet<SkillTypeSO>(), new HashSet<SkillTypeSO>()))
            {
                errors.Add("subSkills contains a cyclic reference.");
            }
        }

        private static bool HasSubSkillCycle(
            SkillTypeSO node,
            HashSet<SkillTypeSO> visiting,
            HashSet<SkillTypeSO> visited)
        {
            if (node == null)
            {
                return false;
            }

            if (visiting.Contains(node))
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            visiting.Add(node);
            if (node.subSkills != null)
            {
                for (int i = 0; i < node.subSkills.Count; i++)
                {
                    if (HasSubSkillCycle(node.subSkills[i], visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(node);
            return false;
        }

        private static void ValidateEffectPrefab(GameObject prefab, string fieldName, List<string> warnings)
        {
            if (prefab == null)
            {
                return;
            }

            if (prefab.GetComponent<SimplePooledParticlePlayer>() == null)
            {
                warnings.Add($"{fieldName} does not contain SimplePooledParticlePlayer. Pool playback can fail at runtime.");
            }
        }

        private void ValidateAnimatorOverride(List<string> errors, List<string> warnings)
        {
            if (animatorOverride == null)
            {
                warnings.Add("animatorOverride is null. Attack will use base controller clip.");
                return;
            }

            var overridePairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animatorOverride.GetOverrides(overridePairs);

            if (overridePairs.Count == 0)
            {
                warnings.Add("animatorOverride has no override entries.");
                return;
            }

            var attackClips = new HashSet<AnimationClip>();
            var fallbackClips = new HashSet<AnimationClip>();

            for (int i = 0; i < overridePairs.Count; i++)
            {
                var original = overridePairs[i].Key;
                var resolved = overridePairs[i].Value != null ? overridePairs[i].Value : overridePairs[i].Key;
                if (resolved == null)
                {
                    continue;
                }

                fallbackClips.Add(resolved);

                bool attackNamed = ContainsAttackWord(original) || ContainsAttackWord(resolved);
                if (attackNamed)
                {
                    attackClips.Add(resolved);
                }
            }

            if (attackClips.Count == 0)
            {
                warnings.Add("No Attack-named clip slot found in animatorOverride. Hit event scan will use all resolved clips.");
                attackClips = fallbackClips;
            }

            bool hasAnyHit = false;
            foreach (var clip in attackClips)
            {
                if (clip == null)
                {
                    continue;
                }

                var events = AnimationUtility.GetAnimationEvents(clip);
                int hitCountInClip = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (string.Equals(evt.functionName, "Hit", StringComparison.Ordinal) ||
                        string.Equals(evt.functionName, "shoot", StringComparison.OrdinalIgnoreCase))
                    {
                        hitCountInClip++;
                        hasAnyHit = true;

                        if (evt.time <= 0f || evt.time >= clip.length)
                        {
                            warnings.Add($"Hit event timing is near clip boundary. clip={clip.name}, time={evt.time:0.###}, length={clip.length:0.###}");
                        }
                    }
                    else if (string.Equals(evt.functionName, "hit", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(evt.functionName, "shoot", StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Animation event function name uses wrong case. Expected 'Hit'. clip={clip.name}, function={evt.functionName}");
                    }
                }

                if (hitCountInClip == 0)
                {
                    errors.Add($"Missing Hit event in attack clip. clip={clip.name}");
                }
                else if (hitCountInClip > 1)
                {
                    warnings.Add($"Multiple Hit events found in clip. clip={clip.name}, count={hitCountInClip}");
                }
            }

            if (!hasAnyHit)
            {
                errors.Add("No valid 'Hit' event found in animatorOverride attack clips.");
            }
        }

        private static bool ContainsAttackWord(AnimationClip clip)
        {
            return clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif
    }
}

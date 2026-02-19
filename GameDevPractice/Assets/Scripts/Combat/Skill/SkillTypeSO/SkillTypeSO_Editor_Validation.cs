#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEditor;
using UnityEngine;

namespace TH.Resource
{
    public partial class SkillTypeSO
    {
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
                Logg.Log($"{prefix} Validation passed.", this);
                return true;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                Logg.LogWarning($"{prefix} Warning: {warnings[i]}", this);
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Logg.LogError($"{prefix} Error: {errors[i]}", this);
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
    }
}
#endif

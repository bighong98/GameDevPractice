#if UNITY_EDITOR
using System.Collections.Generic;
using TH.Combat;
using TH.Utils;
using UnityEditor;
using UnityEngine;

namespace TH.Resource
{
    public partial class SkillTypeSO
    {
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
                Logg.LogWarning($"[SkillTypeSO:{name}] comboSequence preset is null.", this);
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
                Logg.LogWarning($"[SkillTypeSO:{name}] onHitEffects skipped {skippedCount} unsupported effect(s) during auto import.", this);
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
                Logg.LogWarning($"[SkillTypeSO:{name}] onHitEffects is empty.", this);
                return;
            }

            _ = ImportOnHitEffects(out int skippedCount);
            lastImportedOnHitEffectsSignature = ComputeOnHitEffectsSignature();

            if (skippedCount > 0)
            {
                Logg.LogWarning($"[SkillTypeSO:{name}] onHitEffects skipped {skippedCount} unsupported effect(s) during force reimport.", this);
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
    }
}
#endif

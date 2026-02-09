using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "ComboSequenceSO", menuName = "Scriptable Objects/Type/Skill/ComboSequenceSO")]
    public class ComboSequenceSO : ScriptableObject
    {
        [SerializeField, Min(0f)] private float comboTimeout = 0.75f;
        [SerializeField] private List<SkillTypeSO> comboSteps = new();

        public float ComboTimeout => Mathf.Max(0f, comboTimeout);
        public int StepCount => comboSteps?.Count ?? 0;
        public bool HasSteps => StepCount > 0;
        public IReadOnlyList<SkillTypeSO> ComboSteps => comboSteps;
#if UNITY_EDITOR
        [ContextMenu("Validate Combo Sequence (Editor)")]
        private void ValidateSequenceInEditor()
        {
            ValidateAndLogInEditor();
        }

        public bool ValidateAndLogInEditor(string logPrefix = null, bool includeStepSkillValidation = true)
        {
            var isValid = ValidateInEditor(out var errors, out var warnings, includeStepSkillValidation);
            var prefix = string.IsNullOrWhiteSpace(logPrefix)
                ? $"[ComboSequenceSO:{name}]"
                : $"[{logPrefix}][ComboSequenceSO:{name}]";

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

        public bool ValidateInEditor(out List<string> errors, out List<string> warnings, bool includeStepSkillValidation = true)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (float.IsNaN(comboTimeout) || float.IsInfinity(comboTimeout) || comboTimeout < 0f)
            {
                errors.Add("comboTimeout must be a finite value >= 0.");
            }

            if (comboSteps == null || comboSteps.Count == 0)
            {
                errors.Add("comboSteps is empty. Sequence must contain at least one step skill.");
                return false;
            }

            var duplicateCheck = new HashSet<SkillTypeSO>();
            for (int i = 0; i < comboSteps.Count; i++)
            {
                var step = comboSteps[i];
                if (step == null)
                {
                    errors.Add($"comboSteps[{i}] is null.");
                    continue;
                }

                if (!duplicateCheck.Add(step))
                {
                    warnings.Add($"Duplicate step skill reference detected. index={i}, skill={step.name}");
                }

                if (step.ComboSequence != null)
                {
                    warnings.Add($"Step skill has its own comboSequence. index={i}, skill={step.name}");
                }

                if (step.AnimatorOverride == null)
                {
                    warnings.Add($"Step skill has no animator override. index={i}, skill={step.name}");
                }

                if (!includeStepSkillValidation)
                {
                    continue;
                }

                step.ValidateInEditor(out var stepErrors, out var stepWarnings);
                for (int e = 0; e < stepErrors.Count; e++)
                {
                    errors.Add($"Step[{i}] {step.name}: {stepErrors[e]}");
                }

                for (int w = 0; w < stepWarnings.Count; w++)
                {
                    warnings.Add($"Step[{i}] {step.name}: {stepWarnings[w]}");
                }
            }

            return errors.Count == 0;
        }
#endif


        public SkillTypeSO GetStepSkill(int index, SkillTypeSO fallback)
        {
            if (!HasSteps) return fallback;
            if (index < 0 || index >= comboSteps.Count) return fallback;

            return comboSteps[index].IsNotNull() ? comboSteps[index] : fallback;
        }
    }
}

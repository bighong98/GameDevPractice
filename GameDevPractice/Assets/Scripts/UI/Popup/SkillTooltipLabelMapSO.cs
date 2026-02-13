using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.UI
{
    [CreateAssetMenu(fileName = "SkillTooltipLabelMapSO", menuName = "Scriptable Objects/UI/Tooltip/Skill Label Map")]
    public sealed class SkillTooltipLabelMapSO : ScriptableObject
    {
        [Serializable]
        public sealed class LabelEntry
        {
            public string key;
            public string label;
        }

        [SerializeField] private List<LabelEntry> entries = new()
        {
            new LabelEntry { key = TooltipLabelKeys.SkillType, label = "\uC2A4\uD0AC" },
            new LabelEntry { key = TooltipLabelKeys.SkillRange, label = "\uC0AC\uAC70\uB9AC" },
            new LabelEntry { key = TooltipLabelKeys.SkillCooldown, label = "\uCFE8\uD0C0\uC784" },
            new LabelEntry { key = TooltipLabelKeys.SkillHitCount, label = "\uD0C0\uACA9 \uD69F\uC218" },
            new LabelEntry { key = TooltipLabelKeys.SkillAttackCoefficient, label = "\uACF5\uACA9 \uACC4\uC218" }
        };

        private Dictionary<string, string> labelLookup;

        public IReadOnlyList<LabelEntry> Entries => entries;

        public string GetLabel(string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            EnsureLookup();

            if (labelLookup != null && labelLookup.TryGetValue(key, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                return mapped;

            return fallback ?? key;
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookup();
        }
#endif

        private void EnsureLookup()
        {
            if (labelLookup != null)
                return;

            RebuildLookup();
        }

        private void RebuildLookup()
        {
            labelLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.label))
                    continue;

                labelLookup[entry.key] = entry.label;
            }
        }
    }
}

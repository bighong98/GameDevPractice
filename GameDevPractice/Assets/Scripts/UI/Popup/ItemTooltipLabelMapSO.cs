using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.UI
{
    [CreateAssetMenu(fileName = "ItemTooltipLabelMapSO", menuName = "Scriptable Objects/UI/Tooltip/Item Label Map")]
    public sealed class ItemTooltipLabelMapSO : ScriptableObject
    {
        [Serializable]
        public sealed class LabelEntry
        {
            public string key;
            public string label;
        }

        [SerializeField] private List<LabelEntry> entries = new()
        {
            new LabelEntry { key = TooltipLabelKeys.ItemSectionStats, label = "\uB2A5\uB825\uCE58" },
            new LabelEntry { key = TooltipLabelKeys.ItemSectionEffects, label = "\uD6A8\uACFC" },
            new LabelEntry { key = "item.effect.ItemHealEffect.summary", label = "\uCCB4\uB825 \uD68C\uBCF5{effect.heal.amount_suffix}{effect.heal.percent_suffix}" },
            new LabelEntry { key = "item.effect.ItemHealEffect.detail", label = "\uCCB4\uB825 \uD68C\uBCF5{effect.heal.amount_suffix}{effect.heal.percent_suffix}" },
            new LabelEntry { key = "item.effect.ItemManaRecoverEffect.summary", label = "\uB9C8\uB098 \uD68C\uBCF5{effect.mana.amount_suffix}{effect.mana.percent_suffix}" },
            new LabelEntry { key = "item.effect.ItemManaRecoverEffect.detail", label = "\uB9C8\uB098 \uD68C\uBCF5{effect.mana.amount_suffix}{effect.mana.percent_suffix}" },
            new LabelEntry { key = "item.type.Default", label = "\uAE30\uBCF8" },
            new LabelEntry { key = "item.type.Equipment", label = "\uC7A5\uBE44" },
            new LabelEntry { key = TooltipLabelKeys.ItemTypeCountableUsable, label = "\uC18C\uBE44" },
            new LabelEntry { key = TooltipLabelKeys.ItemTypeCountableResource, label = "\uC7AC\uB8CC" },
            new LabelEntry { key = "item.type.Countable", label = "\uC18C\uBAA8" },
            new LabelEntry { key = "item.type.Single", label = "\uB2E8\uC77C" },
            new LabelEntry { key = "item.type.Special", label = "\uD2B9\uC218" }
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

using System.Collections.Generic;
using TH.Item;
using TH.UI;
using UnityEngine;

namespace TH.Item.Data
{
    [CreateAssetMenu(fileName = "ItemManaRecoverEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemManaRecoverEffect")]
    public class ItemManaRecoverEffect : ItemEffectBase, IItemEffectTooltipInfo, ITooltipTokenProvider
    {
        [SerializeField] private int manaAmount;
        [SerializeField] private float manaRatio;

        public override bool TryApply(in ItemUseContext context)
        {
            // Runtime mana recovery behavior is not wired in the current stat system yet.
            // Keep return behavior unchanged while exposing tokenized tooltip values.
            return true;
        }

        public string GetTooltipSummary()
        {
            return BuildTooltipText();
        }

        public string GetTooltipDetail()
        {
            return BuildTooltipText();
        }

        public void CollectTokens(in TooltipTokenContext context, IDictionary<string, TooltipTokenValue> tokens)
        {
            TooltipTokenResolver.SetToken(tokens, "effect.mana.amount", manaAmount);
            TooltipTokenResolver.SetToken(tokens, "effect.mana.ratio", manaRatio);
            TooltipTokenResolver.SetToken(tokens, "effect.mana.percent", manaRatio * 100f);
            TooltipTokenResolver.SetToken(tokens, "effect.mana.amount_suffix", manaAmount > 0 ? $" {manaAmount}" : string.Empty);
            TooltipTokenResolver.SetToken(tokens, "effect.mana.percent_suffix", manaRatio > 0f ? $" (+{manaRatio * 100f:0.##}%)" : string.Empty);
        }

        private string BuildTooltipText()
        {
            bool recoverByAmount = manaAmount > 0;
            bool recoverByRatio = manaRatio > 0f;

            if (recoverByAmount && recoverByRatio)
                return $"Restore Mana {manaAmount} (+{manaRatio * 100f:0.##}%)";
            if (recoverByAmount)
                return $"Restore Mana {manaAmount}";
            if (recoverByRatio)
                return $"Restore Mana +{manaRatio * 100f:0.##}%";

            return "Restore Mana";
        }
    }
}



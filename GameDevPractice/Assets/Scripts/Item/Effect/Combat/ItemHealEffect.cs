using System.Collections.Generic;
using TH.Attribute;
using TH.Combat;
using TH.Item;
using TH.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemHealEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemHealEffect")]
public class ItemHealEffect : ItemEffectBase, IItemEffectTooltipInfo, ITooltipTokenProvider
{
    [SerializeField] int healAmount;
    [SerializeField] float healRatio;
    [SerializeField] private bool healByForce;

    public override bool TryApply(in ItemUseContext context)
    {
        if (context.User is not IHealable target || !target.IsNotNull())
        {
            return false;
        }

        return TryHeal(target);
    }

    private bool TryHeal(IHealable target)
    {
        bool healByAmount = healAmount > 0;
        bool healByRatio = healRatio > 0;
        if (!healByAmount && !healByRatio) return false;

        if (healByAmount) target.Heal(healAmount, healByForce);
        if (healByRatio) target.HealRatio(healRatio, healByForce);

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
        TooltipTokenResolver.SetToken(tokens, "effect.heal.amount", healAmount);
        TooltipTokenResolver.SetToken(tokens, "effect.heal.ratio", healRatio);
        TooltipTokenResolver.SetToken(tokens, "effect.heal.percent", healRatio * 100f);
        TooltipTokenResolver.SetToken(tokens, "effect.heal.amount_suffix", healAmount > 0 ? $" {healAmount}" : string.Empty);
        TooltipTokenResolver.SetToken(tokens, "effect.heal.percent_suffix", healRatio > 0f ? $" (+{healRatio * 100f:0.##}%)" : string.Empty);
    }

    private string BuildTooltipText()
    {
        bool healByAmount = healAmount > 0;
        bool healByRatio = healRatio > 0;

        if (healByAmount && healByRatio)
            return $"Heal {healAmount} (+{healRatio * 100f:0.##}%)";
        if (healByAmount)
            return $"Heal {healAmount}";
        if (healByRatio)
            return $"Heal +{healRatio * 100f:0.##}%";

        return "Heal";
    }
}


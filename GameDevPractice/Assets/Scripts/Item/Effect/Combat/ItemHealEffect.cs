using TH.Attribute;
using TH.Combat;
using TH.Item;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemHealEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemHealEffect")]
public class ItemHealEffect : ItemEffectBase, IItemEffectTooltipInfo
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

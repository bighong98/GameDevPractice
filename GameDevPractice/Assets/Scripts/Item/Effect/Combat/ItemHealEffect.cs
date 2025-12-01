using TH.Attribute;
using TH.Combat;
using TH.Item;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemHealEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemHealEffect")]
public class ItemHealEffect : ItemEffectBase
{
    [SerializeField] int healAmount;
    [SerializeField] float healRatio;

    public override bool TryApply(in ItemUseContext context)
    {
        if (context.User is not IHealable target || !target.IsAlive()
            || context.Item is not { IsValid: true, IsEmpty: false } item
            || context.Slot == null || context.Storage == null)
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

        if (healByAmount) target.Heal(healAmount);
        if (healByRatio) target.HealRatio(healRatio);

        return true;
    }
}

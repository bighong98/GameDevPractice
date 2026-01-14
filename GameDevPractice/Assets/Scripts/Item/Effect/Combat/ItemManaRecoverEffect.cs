using TH.Item;
using UnityEngine;

namespace TH.Item.Data
{
    [CreateAssetMenu(fileName = "ItemManaRecoverEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemManaRecoverEffect")]
    public class ItemManaRecoverEffect : ItemEffectBase, IItemEffectTooltipInfo
    {
        public override bool TryApply(in ItemUseContext context)
        {
            return true;
        }

        public string GetTooltipSummary()
        {
            return "Restore Mana";
        }

        public string GetTooltipDetail()
        {
            return "Restore Mana";
        }
    }
}


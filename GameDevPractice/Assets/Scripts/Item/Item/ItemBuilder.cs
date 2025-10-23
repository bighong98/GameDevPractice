using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public class ItemBuilder : IItemBuilder
    {
        public IGameItem GetItemFromData(ItemTypeSO data, int amount = 1)
        {
            if (data is not { itemType: { } type })
            {
                Logg.LogError($"[PlayerInventory] failed to Make GameItem Instance");
                return null;
            }
            switch (type)
            {
                case Enums.ItemType.Countable:
                    if (data.isUsable) return new ConsumableItem(data, amount);
                    return new CountableItem(data, amount);
                case Enums.ItemType.Equipment:
                    return new EquipmentItem(data);
                default:
                    return new GameItem(data);
            }
        }
    }
}


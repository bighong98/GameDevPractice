using TH.Utils;
using UnityEngine;
using TH.Resource;

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
                    return new CountableItem(data, amount);
                case Enums.ItemType.Equipment:
                    return new EquipmentItem(data);
                default:
                    return new GameItem(data);
            }
        }
    }
}


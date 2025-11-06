using UnityEngine;
using TH.Resource;

namespace TH.Item
{
    public sealed class EquipmentSlot : ItemSlot
    {
        private readonly Enums.EquippedItemSlotType ValidEquipSlotType;
        
        public EquipmentSlot() : base() { }

        public EquipmentSlot(int index, Enums.EquippedItemSlotType slotType, IGameItem item = null, bool accessible = true, bool visible = true) : base(index, item, accessible: accessible, visible: visible)
        {
            base.ValidTypes = new Enums.ItemType[] { Enums.ItemType.Equipment };
            ValidEquipSlotType = slotType;
        }

        public override bool CanStore(ItemTypeSO itemData)
        {
            if (!IsAccessible) return false;
            if (itemData is not EquipmentTypeSO equipmentData) return false;

            var type = equipmentData.itemType;
            var slotType = equipmentData.slotType;

            foreach (var expected in ValidTypes)
            {
                if (type == expected) break;
                return false;
            }

            return ValidEquipSlotType == slotType;
        }
    }
}

using UnityEngine;

namespace RPG.Item
{
    public class EquipmentItem : Item
    {
        public EquipmentItem() {}
        public EquipmentItem(ItemTypeSO data) : base(data)
        {
            
        }
        protected bool isEquipped;
        
        public bool IsEquipped => isEquipped;
        
        public bool Equip() // todo: 장비 장착의 주체 추가
        {
            if (isEquipped) return false;
        
            isEquipped = true;
            return true;
        }

        public bool UnEquip() // todo: 장비 장착의 주체 제거
        {
            if (!isEquipped) return false;

            isEquipped = false;
            return false;
        }
    }
}


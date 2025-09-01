using System;
using UnityEngine;

namespace RPG.Item
{
    // 장비 장착 구현 목적의 아이템 슬롯
    // 특정한 부위의 장비 아이템만 저장 가능
    // 장착(장비의 능력치 적용)이 필요한 경우가 아니라면 ItemSlot 혹은 파생클래스 사용할 것
    public class EquipmentSlot : ItemSlot
    {
        public EquipmentSlot(Item item, int index, Enums.ItemType[] validTypes = null, bool accessible = true, 
            Enums.EquippedItemSlotType validEquipSlotType = Enums.EquippedItemSlotType.Max) 
            : base(item, index, validTypes, accessible)
        {
            ValidEquipSlotType = validEquipSlotType;
        }

        protected Enums.EquippedItemSlotType ValidEquipSlotType; // 슬롯에 장착 가능한 장비군(무기, 머리, 몸, 손, 발, 등)

        public event EventHandler<EquipmentSlotArgs> OnEquipmentChanged; // 장착, 장착해제 전달용 이벤트핸들러
        
        public override bool CanStore(ItemTypeSO itemData)
        {
            if (base.CanStore(itemData))
            {
                if (itemData is EquipmentTypeSO equipmentData)
                {
                    return ValidEquipSlotType == equipmentData.slotType;
                }
            }

            return false;
        }

        public override bool Store(Item item, bool byForce = false)
        {
            var prevItem = this.Item;
            
            if (base.Store(item, byForce))
            {
                if (prevItem is { IsValid: true }) // 기존에 슬롯에 장착되었던 아이템이 있었다면
                {
                    // 기존 장비 장착해제 이벤트 전달
                    Util.Log($"[EquipmentSlot[{Index}]]: Item UnEquipped", Util.LoggingMode.Completed);
                    OnEquipmentChanged?.Invoke(this, new EquipmentSlotArgs(prevItem, EquipmentSlotArgs.EquipEventState.UnEquip));
                }
                // 새 장비 장착 이벤트 전달
                Util.Log($"[EquipmentSlot[{Index}]]: New Item Equipped", Util.LoggingMode.Completed);
                OnEquipmentChanged?.Invoke(this, new EquipmentSlotArgs(this.Item, EquipmentSlotArgs.EquipEventState.Equip));
                return true;
            }

            return false;
        }
    }

    // 장비 장착/장착해제 여부 전달 목적 EventArgs
    public class EquipmentSlotArgs : EventArgs
    {
        public Item Item;
        public EquipEventState State;

        public EquipmentSlotArgs(Item item, EquipEventState state)
        {
            this.Item = item;
            this.State = state;
        }
        public enum EquipEventState
        {
            Equip,
            UnEquip,
        }
    }
}


using System.Collections.Generic;
using RPG.Combat;
using TH.Attribute.Stat;
using TH.Item;
using TH.Utils;
using UnityEngine;

namespace RPG.Item
{
    public class EquipmentItem : Item, IUsableItem, IEquipment
    {
        public EquipmentItem() {}
        public EquipmentItem(ItemTypeSO data) : base(data)
        {
            if (data is EquipmentTypeSO equipmentData)
            {
                equipmentStats = equipmentData.equipmentStats;
            }
        }
        
        public object Owner => owner;
        object owner;
        private bool IsEquipped => owner != null;

        private readonly List<StatModifierData> equipmentStats;
        
        public bool Equip(object own)
        {
            // 이미 장착 중인 아이템이 존재하거나, 장착 대상을 찾지 못한 경우 false 
            if (IsEquipped || !TryGetStatHolder(own, out IStatHolder statHolder)) return false; 
            owner = own;
            
            if (equipmentStats is { Count: > 0 }) // 아이템에 스탯이 존재한다면 아이템 능력치 적용
            {
                foreach (var statInfo in equipmentStats)
                {
                    var mod = statInfo.GetModifier(this); 
                    statHolder.AddModifier(statInfo.type, mod);
                }
            }

            if (GetItemInfo is WeaponTypeSO weapon && own is Component c && c.TryGetComponent(out Fighter oFighter))
            {
                oFighter.EquipWeapon(weapon); // 무기라면 Fighter에게 무기 정보 전달
            }

            return true;
        }

        public bool UnEquip()
        {
            if (!IsEquipped) return false; // 장착 중인 아이템이 존재하지 않을 경우 false 
            if (!TryGetStatHolder(owner, out IStatHolder statHolder)) return false; // owner로부터 IStatHolder를 찾지 못했을 경우 false
            if (!statHolder.RemoveModifier(this)) return false; // 장비 스탯 적용 해제에 실패한 경우 false
            
            if (GetItemInfo is WeaponTypeSO weapon && owner is Component c && c.TryGetComponent(out Fighter oFighter))
            {
                oFighter.UnEquipWeapon();
            }
            
            owner = null;
            return true;
        }

        public bool Use() // 장비 장착 해제 시도
        {
            return IsEquipped && UnEquip();
        }

        public bool Use(object user) // 장비 장착으로 적용
        {
            return Equip(own: user);
        }

        private bool TryGetStatHolder(object from, out IStatHolder statHolder)
        {
            if (from is IStatHolder fHolder)
            {
                statHolder = fHolder;
                return true;
            }
            
            if (from is Component c && c.TryGetComponent(out IStatHolder cHolder))
            {
                statHolder = cHolder;
                return true;
            }

            Logg.Log($"[{GetItemInfo.nameString}] failed to TryGetStatHolder", Logg.LoggingMode.InProgress);
            statHolder = default;
            return false;
        }
    }
}


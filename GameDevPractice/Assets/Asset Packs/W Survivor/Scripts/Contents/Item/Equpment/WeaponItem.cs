using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponItem : EquipmentItem
{
    public WeaponItem(int id, int optionGroup, string name, string desc, string itemSprite, string dropSprite) 
        : base(id, (int)Enums.ItemType.Equipment, optionGroup, name, desc, itemSprite, dropSprite)
    {
        EquipmentType = (int)Enums.EquipmentType.Weapon;
    }

    public override bool Equip()
    {
        if (base.Equip() == false)
            return false;

        return true;
    }

    public override bool UnEquip()
    {
        if (base.UnEquip() == false)
            return false;

        return true;
    }
}

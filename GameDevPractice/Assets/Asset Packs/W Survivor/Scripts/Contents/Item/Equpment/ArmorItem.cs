using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmorItem : EquipmentItem
{
    public int ArmorType; //Enums.ArmorType
    public ArmorItem(int id, int optionGroup, string name, string desc, string itemSprite, string dropSprite, int armorType) 
        : base(id, (int)Enums.ItemType.Equipment, optionGroup, name, desc, itemSprite, dropSprite)
    {
        EquipmentType = (int)Enums.EquipmentType.Armor;
        ArmorType = armorType;
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

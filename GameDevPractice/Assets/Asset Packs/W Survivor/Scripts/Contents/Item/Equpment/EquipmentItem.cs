using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TH.Attribute.Stat;

public abstract class EquipmentItem : BaseItem
{
    public int EquipmentType; // Enums.EquipmentType
    public StatModifier[] Mods;
    public int[] ModTypes;
    protected EquipmentItem(int id, int type, int optionGroup, string name, string desc, string itemSprite, string dropSprite) 
        : base (id, type, optionGroup, name, desc, itemSprite, dropSprite, true) // EquipmentItem must be usable 
    {
        #region For Test

        Mods = new[] { new StatModifier(10, StatModCalcType.Add, this) };
        ModTypes = new[] { (int)Enums.StatType.Attack };
        
        #endregion
    }

    public virtual bool Equip()
    {
        for (int i = 0; i < Mods.Length; i++)
        {
            if (Mods[i] == null)
                continue;

            // InGameManager.Instance.player.playerStat.ModifyStat(Mods[i], ModTypes[i], true);
        }
        
        return true;
    }

    public virtual bool UnEquip()
    {
        for (int i = 0; i < Mods.Length; i++)
        {
            if (Mods[i] == null)
                continue;
            // if (InGameManager.Instance.player.playerStat.ModifyStat(Mods[i], ModTypes[i], false))
            //     continue;
            
            return false; // 옵션 적용 해제 실패시 장착해제 실패 처리
        }

        return true;
    }
}

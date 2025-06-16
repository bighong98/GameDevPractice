using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialItem : BaseItem
{
    public SpecialItem(int id, int optionGroup, string name, string desc, string itemSprite, string dropSprite, bool isUsable) 
        : base (id, (int)Enums.ItemType.Special, optionGroup,  name, desc, itemSprite, dropSprite, isUsable)
    {
        
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseItem
{
    public int ItemId;
    public int ItemType; // Enums.ItemType
    public int ItemOptionGroup;
    public string ItemName;
    public string ItemDesc;
    public string ItemSpriteName;
    public string DropSpriteName;
    public bool IsUsable;
    
    // public int ItemId => _itemId;
    // public int ItemType => _itemType; // Enums.ItemType
    // public int ItemOptionGroup => _itemOptionGroup;
    // public string ItemName => _itemName;
    // public string ItemDesc => _itemDesc;
    // public string ItemSpriteName => _itemSpriteName;
    // public string DropSpriteName => _dropSpriteName;
    // public bool IsUsable => _isUsable;
    
    public BaseItem(int id, int type, int optionGroup, string nameText, string desc, string itemSprite, string dropSprite, bool isUsable)
    {
        ItemId = id;
        ItemType = type;
        ItemOptionGroup = optionGroup;
        ItemName = nameText;
        ItemDesc = desc;
        ItemSpriteName = itemSprite;
        DropSpriteName = dropSprite;
        IsUsable = isUsable;
    }

    public virtual T Clone<T>(int amount = 1) where T : BaseItem 
    {
        return (T)this.MemberwiseClone();
    }
}

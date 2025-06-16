using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDataHandler
{
    // private string tableKey = "";
    private TextAsset itemDataText;
    private ItemDatas itemDatas;

    public void Init()
    {
        // itemDataText = Managers.Resource.Load<TextAsset>(tableKey);
        // itemDatas = JsonUtility.FromJson<ItemDatas>(itemDataText?.text);
        
        itemDatas = new ItemDatas
        {
            ItemDataTable = new[] { 
                new ItemData(0, "gem_0.sprite", (int)Enums.ItemType.Countable, "test_item_name", "test_item_desc", 0, 0000, 0), 
                new ItemData(1, "Props.multiSprite[Health]", (int)Enums.ItemType.Countable, "HealingPotion", "HealingPotion", 0, 1001, 0),
                new ItemData(2, "Props.multiSprite[Weapon 0]", (int)Enums.ItemType.Equipment, "WeaponSample", "Weapon Sample Desc", 0, 2001, 0),
                new ItemData(3, "Props.multiSprite[Mag]", (int)Enums.ItemType.Equipment, "HandSample", "Hand Sample Desc", 0, 3001, 3),
            }
        };
    }

    public ItemData[] GetData()
    {
        return itemDatas.ItemDataTable;
    }
}

[System.Serializable]
public class ItemDatas
{
    public ItemData[] ItemDataTable;
}

[System.Serializable]
public class ItemData
{
    public int tblidx;
    public string item_code; // item_code is key for addressable (loading asset)
    public int item_type;
    public string item_name;
    public string item_desc;
    public int grade;
    public int option_group;
    public int item_sub_type;

    // constructor for test
    public ItemData(
        int tblidx,
        string item_code,
        int item_type,
        string item_name,
        string item_desc,
        int grade,
        int option_group,
        int item_sub_type)
    {
        this.tblidx = tblidx;
        this.item_code = item_code;
        this.item_type = item_type;
        this.item_name = item_name;
        this.item_desc = item_desc;
        this.grade = grade;
        this.option_group = option_group;
        this.item_sub_type = item_sub_type;
    }
}
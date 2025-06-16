// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class ItemSpawner
// {
//     private Dictionary<int, int> _itemPoolDictionary = new Dictionary<int, int>(); // <tblidx, item pool index> 
//     private Dictionary<string, int> _itemIdxDictionary = new Dictionary<string, int>(); // <item code, tblidx>
//     private ItemDataHandler _itemDataHandler = new ItemDataHandler();
//     private ItemData[] _itemData;
//     public Transform dropItemContainer; // public for debugging
//     
//     public bool Init() // Init() is called by InGameManager
//     {
//         _itemDataHandler.Init();
//         _itemData = _itemDataHandler.GetData();
//
//         for (int i = 0; i < _itemData.Length; i++)
//             _itemIdxDictionary.Add(_itemData[i].item_code, _itemData[i].tblidx);
//         
//         Transform dropContainer = InGameManager.Instance.DropContainer;
//         if (dropContainer == null)
//         {
//             Util.Log("ItemSpawner: failed to find drop container");
//         }
//
//         dropItemContainer = dropContainer.Find("Item");
//         if (dropItemContainer == null)
//         {
//             GameObject go = new GameObject() { name = "Item" };
//             dropItemContainer = go.transform;
//             dropItemContainer.SetParent(dropContainer);
//         }
//         return true;
//     }
//
//     public DropItemController SpawnItem(int itemTableIdx, Vector3 pos)
//     {
//         if (_itemPoolDictionary.TryGetValue(itemTableIdx, out int poolIdx)) // 이미 동일 아이템이 생성되었던 경우
//             return Managers.Pool.GetFromPool<DropItemController>(poolIdx, pos);
//         
//         BaseItem item = ExtractItemData(itemTableIdx);
//         if (item == null)
//             return null;
//         
//         GameObject dropItemGo = new GameObject() { name = item.ItemName };
//         dropItemGo.transform.SetParent(dropItemContainer);
//         dropItemGo.transform.position = pos;
//         DropItemController dropItem = dropItemGo.AddComponent<DropItemController>().SetItem(item);
//         
//         Managers.Pool.AddPool<DropItemController>(
//             source: dropItem,
//             count: 99, // 99 is magic number
//             container: dropItemContainer,
//             out int newPoolIdx
//         );
//         _itemPoolDictionary.Add(itemTableIdx, newPoolIdx);
//         
//         return dropItem;
//     }
//
//     public DropItemController SpawnItem(string itemCode, Vector3 pos) // itemKey is item_code
//     {
//         if (_itemIdxDictionary.TryGetValue(itemCode, out int tblidx))
//         {
//             return SpawnItem(tblidx, pos);
//         }
//         
//         Util.Log("ItemSpawner: failed to spawn Item. invalid item code");
//         return null;
//     }
//     
//     public BaseItem ExtractItemData(int itemIdx) // 아이템을 필드에 스폰시키지 않고 생성
//     {
//         ItemData itemData = _itemData[itemIdx];
//         bool isUsable = itemData.option_group >= 1000; // option_group 1000 미만은 미사용 아이템
//         
//         switch (itemData.item_type)
//         {
//             case (int)Enums.ItemType.Countable:
//                 CountableItem countItem =
//                     new CountableItem(
//                         id: itemData.tblidx,
//                         optionGroup: itemData.option_group,
//                         name: itemData.item_name, 
//                         desc: itemData.item_desc,
//                         itemSprite: itemData.item_code,
//                         dropSprite: itemData.item_code,
//                         isUsable,
//                         maxAmount: 99
//                         );
//                 return countItem;
//             case (int)Enums.ItemType.Single:
//                 SingleItem singleItem =
//                     new SingleItem(
//                         id: itemData.tblidx,
//                         optionGroup: itemData.option_group,
//                         name: itemData.item_name, 
//                         desc: itemData.item_desc,
//                         itemSprite: itemData.item_code,
//                         dropSprite: itemData.item_code,
//                         isUsable
//                         );
//                 return singleItem;
//             case (int)Enums.ItemType.Special:
//                 SpecialItem specialItem = new SpecialItem(
//                     id: itemData.tblidx,
//                     optionGroup: itemData.option_group,
//                     name: itemData.item_name, 
//                     desc: itemData.item_desc,
//                     itemSprite: itemData.item_code,
//                     dropSprite: itemData.item_code,
//                     isUsable
//                     );
//                 return specialItem;
//             case (int)Enums.ItemType.Equipment:
//                 switch (itemData.item_sub_type)
//                 {
//                     case (int)Enums.EquippedItemSlotType.Weapon:
//                         WeaponItem weaponItem = new WeaponItem(
//                             id: itemData.tblidx,
//                             optionGroup: itemData.option_group,
//                             name: itemData.item_name, 
//                             desc: itemData.item_desc,
//                             itemSprite: itemData.item_code,
//                             dropSprite: itemData.item_code
//                         );
//                         return weaponItem;
//                     
//                     case (int)Enums.EquippedItemSlotType.Head:
//                     case (int)Enums.EquippedItemSlotType.Body:
//                     case (int)Enums.EquippedItemSlotType.Hand:
//                     case (int)Enums.EquippedItemSlotType.Foot:
//                         ArmorItem armorItem = new ArmorItem(
//                             id: itemData.tblidx,
//                             optionGroup: itemData.option_group,
//                             name: itemData.item_name, 
//                             desc: itemData.item_desc,
//                             itemSprite: itemData.item_code,
//                             dropSprite: itemData.item_code,
//                             armorType: itemData.item_sub_type - (int)Enums.EquippedItemSlotType.Head
//                         );
//                         return armorItem;
//                     default:
//                         return null;
//                 }
//             default:
//                 Util.Log("ItemSpawner: UnValid itemType");
//                 return null;
//         }
//     }
//
//     public BaseItem ExtractItemData(string itemCode) // 아이템을 필드에 스폰시키지 않고 생성
//     {
//         if (_itemIdxDictionary.TryGetValue(itemCode, out int tblidx))
//             return ExtractItemData(tblidx);
//         
//         Util.Log("ItemSpawner: Failed to get item. invalid itemCode");
//         return null;
//     }
//     
// }

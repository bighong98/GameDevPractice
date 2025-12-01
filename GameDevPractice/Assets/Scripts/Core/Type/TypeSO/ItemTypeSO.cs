using System.Collections.Generic;
using TH.Item;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "ItemTypeSO", menuName = "Scriptable Objects/Type/Item/ItemTypeSO")]
    public class ItemTypeSO : BaseTypeSO
    {
        [Header("Item Info")] 
        public Enums.ItemType itemType;
        public int maxAmount = 1; // default: 1
        public string desc;
        public bool isUsable => itemType == Enums.ItemType.Equipment || itemUseEffects.Count > 0; // Usable = Consumable(소비 가능) + Equipable(장착 가능)
        public List<ItemEffectBase> itemUseEffects;
    }
}


using System;
using UnityEngine;

namespace TH.Item
{
    [Serializable] // serializable for save/load
    public class GameItem : IGameItem
    {
        [SerializeField] protected ItemTypeSO itemData;
        [SerializeField] protected int amount;

        public GameItem() { amount = 1; } // empty constructor for serialization

        public GameItem(ItemTypeSO data)
        {
            itemData = data;
            amount = 1;
        }

        public ItemTypeSO GetItemInfo => itemData;
        public Enums.ItemType Type => itemData == null ? Enums.ItemType.Default : itemData.itemType;

        public int GetAmount => amount;
        public bool IsValid => itemData != null && amount > 0;
        public bool IsEmpty => amount <= 0 || itemData == null;
        
        public virtual T Clone<T>() where T : IGameItem
        {
            return (T)this.MemberwiseClone();
        }
    }
}


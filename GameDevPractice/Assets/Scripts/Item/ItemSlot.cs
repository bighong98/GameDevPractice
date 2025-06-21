using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Item
{
    public class ItemSlot
    {
        protected ItemTypeSO ItemData; // 실제 아이템 데이터 (Scriptable Object) //todo: readonly 고려
        protected int Amount = 1; // 아이템 개수 (기본값: 1)
        protected int Index = -1; // Default: -1 (means not initialized)
        protected bool hasItem = false;
        // 생성자

        public ItemSlot()
        {
            hasItem = false;
        }
        public ItemSlot(ItemTypeSO data)
        {
            ItemData = data;
            hasItem = true;
        }
        
        // 외부 접근용 프로퍼티
        public ItemTypeSO GetItemInfo => ItemData;
        public int GetAmount => Amount;
        public int GetIndex => Index;

        public void SetIndex(int index)
        {
            Index = index;
        }
        public bool IsEmpty => !hasItem || Amount <= 0;
        
        public virtual T Clone<T>() where T : ItemSlot
        {
            return (T)this.MemberwiseClone();
        }
    }
}


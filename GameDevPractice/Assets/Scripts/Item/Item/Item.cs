using System;
using UnityEngine;
using TH.Resource;

namespace RPG.Item
{
    [Serializable] // Serializable for Save/Load
    public class Item
    {
        [SerializeField] protected ItemTypeSO ItemData; // 실제 아이템 데이터 (Scriptable Object) //todo: readonly 고려
        [SerializeField] protected int Amount = 1; // 아이템 개수 (기본값: 1) -> 0인 경우 임의로 빈 슬롯으로 만들었음을 의미함
            
        // 생성자
        public Item() {}
        public Item(ItemTypeSO data)
        {
            ItemData = data;
        }
        
        // 프로퍼티
        public ItemTypeSO GetItemInfo => ItemData;
        public Enums.ItemType Type => ItemData?.itemType ?? Enums.ItemType.Default; // Default means Item is not valid
        public int GetAmount => Amount;
        public bool IsValid => ItemData != null && Amount > 0;
        public bool IsEmpty => Amount <= 0 || ItemData == null;
        
        public virtual T Clone<T>() where T : Item 
        {
            // 데이터 복사, 슬롯 기본 설정 복사 등에 사용되는 자가복제 매서드
            // 특정 아이템타입 슬롯이 복제 과정에서 추가 기능이 필요하다면 오버라이드해서 사용
            return (T)this.MemberwiseClone();
        }
    }
}


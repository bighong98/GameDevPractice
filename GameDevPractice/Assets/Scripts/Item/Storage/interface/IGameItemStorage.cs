using UnityEngine;
using System;
using System.Collections.Generic;

namespace TH.Item
{
    public interface IGameItemStorage
    {
        // delegate event
        event Action<IGameItemSlot> OnSlotChanged;
        event Action OnStorageChanged; // 전체 슬롯 데이터 갱신 
        
        // collection (readonly)
        IReadOnlyCollection<IGameItemSlot> ItemSlots { get; } // 읽기 전용 아이템 슬롯 목록 (Count 등 프로퍼티 및 foreach 사용 목적)
        public int Capacity { get; }
        
        // write/store
        bool TryStore(IGameItem item); // 빈슬롯/적절한 슬롯에 보관, 초과분 버림
        bool TryStore(IGameItem item, out IGameItemSlot storedSlot); // 빈슬롯/유효 슬롯에 보관 + 보관된 슬롯 참조 반환 
        bool TryStore(IGameItem item, int index); // 특정 슬롯에 보관
        

        // read/get
        bool TryGetItem(int index, out IGameItem item);
        bool TryGetItemSlot(int index, out IGameItemSlot itemSlot);
        
        // delete/remove
        bool TryRemoveItem(int index); // 단순 아이템 제거
        bool TryRemoveItem(int index, out IGameItem item); // 아이템 제거 후 제거된 아이템 확인 (아이템 이동 등에 사용)
    }
}


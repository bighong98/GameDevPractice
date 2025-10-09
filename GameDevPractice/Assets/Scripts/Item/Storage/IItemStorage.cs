using UnityEngine;
using System;
using System.Collections.Generic;

namespace TH.Item
{
    public interface IItemStorage
    {
        // delegate event
        event Action<int> OnStoredItemChanged; // 특정 슬롯 데이터 갱신
        event Action OnStoreChanged; // 전체 슬롯 데이터 갱신 
        
        // collection (readonly)
        IReadOnlyCollection<IGameItemSlot> ItemSlots { get; } // 읽기 전용 아이템 슬롯 목록 (Count 등 프로퍼티 및 foreach 사용 목적)
        
        // write/store
        bool TryStore(IGameItem item);
        bool TryStore(IGameItem item, out int excess);
        bool TryStore(IGameItem item, int index);
        bool TryStore(IGameItem item, int index, out int excess);

        // read/get
        bool TryGetItem(int index, out IGameItem item);
        bool TryGetItem(object key, out IGameItem item);
        
        // delete/remove
        bool TryRemoveItem(int index); // 단순 아이템 제거
        bool TryRemoveItem(object key);
        bool TryRemoveItem(int index, out IGameItem item); // 아이템 제거 후 제거된 아이템 확인
        bool TryRemoveItem(object key, out IGameItem item);
    }
}


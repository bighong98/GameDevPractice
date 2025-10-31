using UnityEngine;

namespace TH.Item
{
    public interface IGameItemSlot
    {
        int Index { get; } // IItemStorage 내에서 인덱스
        
        // read slot information
        IGameItem GetItem { get; } // 아이템 인스턴스
        ItemTypeSO GetItemInfo { get; } // 아이템 데이터 Scriptable Object 
        int GetAmount { get; } // 슬롯에 저장된 아이템의 개수
        
        // read slot state
        bool IsAccessible { get; } // 슬롯 접근 가능 여부 반환
        bool IsVisible { get; } // 슬롯 가시 여부 반환
        bool IsValid { get; } // 슬롯 초기화 여부 반환
        bool HasItem { get; } // 슬롯 내 아이템 존재 여부 반환

        // setter
        void SetIndex(int idx);
        void SetAccessibility(bool state);
        void SetVisibility(bool state);

        // add/store
        bool CanStore(ItemTypeSO itemData);
        bool TryStore(IGameItem item, bool byForce = false);
        bool TryStore(IGameItem item, out IGameItem prevItem, bool byForce = false);
        
        // remove/clear
        bool Clear(bool byForce = false);
        bool Clear(out IGameItem stored, bool byForce = false);
    }
}



using UnityEngine;

namespace TH.Item
{
    public interface IGameItemSlot
    {
        int Index { get; } // IItemStorage 내에서 인덱스
        IGameItem GetItem { get; }
        ItemTypeSO GetItemInfo { get; }
        int GetAmount { get; }
        
        bool IsAccessible { get; } // 슬롯 접근 가능 여부
        bool IsVisible { get; } // 슬롯 가시 여부
        bool IsValid { get; } // 슬롯 초기화 여부
        bool HasItem { get; } // 슬롯 내 아이템 존재 여부

        void SetIndex(int idx);
        void SetAccessibility(bool state);
        void SetVisibility(bool state);

        bool CanStore(ItemTypeSO itemData);
        bool TryStore(IGameItem item, bool byForce = false);
        bool TryStore(IGameItem item, out IGameItem prevItem, bool byForce = false);
        
        bool Clear();
    }
}



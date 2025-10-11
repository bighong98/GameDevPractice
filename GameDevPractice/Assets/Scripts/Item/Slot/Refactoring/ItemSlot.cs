using UnityEngine;

namespace TH.Item
{
    public class ItemSlot: IGameItemSlot
    {
        [SerializeField] protected IGameItem Item;
        [SerializeField] private int index; // serialize for debug
        [SerializeField] private Enums.ItemType[] ValidTypes;
        public int Index { get; protected set; }
        public IGameItem GetItem => Item;
        public ItemTypeSO GetItemInfo => Item?.GetItemInfo;
        public int GetAmount { get; protected set;}
        public bool IsAccessible { get; protected set; }
        public bool IsVisible { get; protected set;}
        public bool IsValid => Item != null && Index >= 0;
        public bool HasItem => Item is { GetAmount: > 0 };
        
        public ItemSlot() {}
        public ItemSlot(IGameItem item = null, Enums.ItemType[] validTypes = null, bool accessible = true, bool visible = true)
        {
            Item = item;
            ValidTypes = validTypes;
            IsAccessible = accessible;
            IsVisible = visible;
        }
        public ItemSlot(int index, IGameItem item = null, Enums.ItemType[] validTypes = null, bool accessible = true, bool visible = true)
        {
            Index = index;
            Item = item;
            ValidTypes = validTypes;
            IsAccessible = accessible;
            IsVisible = visible;
        }

        public void SetIndex(int idx) => Index = idx;
        public void SetAccessibility(bool state) => IsAccessible = state;
        public void SetVisibility(bool state) => IsVisible = state;

        public bool CanStore(ItemTypeSO itemData)
        {
            if (!IsValid || !IsAccessible) return false; // 접근 제한된 슬롯이면 실패처리
            if (ValidTypes == null || ValidTypes.Length == 0) return false; // 유효 아이템 타입이 없거나 타입 배열이 초기화되지 않았다면 실패처리
            // if (ValidTypes is not { Length: > 0 }) return false;

            var type = itemData.itemType;
            foreach (var expected in ValidTypes)
            {
                if (type == expected) return true;
            }

            return false;
        }

        public bool TryStore(IGameItem item, bool byForce = false)
        {
            if (!CanStore(item.GetItemInfo) && !byForce) return false;

            this.Item = item;
            return true;
        }

        public bool TryStore(IGameItem item, out IGameItem prevItem, bool byForce = false)
        {
            prevItem = this.Item;
            return TryStore(item, byForce: byForce);
        }

        public bool Clear(bool byForce = false)
        {
            if (!IsAccessible && !byForce) return false; // 접근 불가능한 슬롯이고 강제가 아니라면 실패

            Item = null;
            return true;
        }

        public bool Clear(out IGameItem stored, bool byForce = false)
        {
            stored = this.Item;
            return Clear(byForce: byForce);
        }
    }
}


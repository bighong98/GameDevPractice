using TH.Resource;

namespace TH.Item
{
    // 아이템/슬롯 조회 유틸리티 partial
    public sealed partial class PlayerStorage
    {
        #region Get (Find)

        // 슬롯 인덱스 기반 아이템 조회
        public bool TryGetItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) 
                && slots[index] is {IsAccessible: true, HasItem: true, GetItem: {} slotItem })
            {
                item = slotItem;
                return true;
            }
            
            item = default;
            return false;
        }

        // 슬롯 인덱스 기반 슬롯 참조 조회
        public bool TryGetItemSlot(int index, out IGameItemSlot itemSlot)
        {
            if (IsValidSlotIdx(index) && slots[index] is { IsAccessible: true } slot)
            {
                itemSlot = slot;
                return true;
            }

            itemSlot = default;
            return false;
        }

        // 타입 기준 첫 매칭 슬롯 탐색
        private bool TryFindSlot(ItemTypeSO itemInfo, out IGameItemSlot foundSlot)
        {
            foundSlot = null;
            if (itemInfo == null)
                return false;

            foreach (var slot in slots)
            {
                if (slot is { IsAccessible: true, HasItem: true, GetItemInfo: {} slotItemInfo }
                    && slotItemInfo == itemInfo)
                {
                    foundSlot = slot;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}

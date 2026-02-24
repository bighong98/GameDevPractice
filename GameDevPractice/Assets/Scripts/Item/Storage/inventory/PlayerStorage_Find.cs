using TH.Resource;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Get (Find)

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

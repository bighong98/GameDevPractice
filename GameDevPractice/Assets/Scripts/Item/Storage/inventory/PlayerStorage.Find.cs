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

        #endregion
    }
}

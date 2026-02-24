using System.Linq;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Store

        private bool TryStoreInternal(IGameItem item, int index)
        {
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: false } slot) return false;

            var result = slot.TryStore(item);
            if (result) 
            {
                CacheAdd(item, index);
                NotifySlotChanged(index);
            }
            return result;
        } 

        public bool TryStore(IGameItem item)
        {
            if (EnsureItemInstanceByType(item) is not { } modified) return false;

            if (modified is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount <= 0) return false;

                return TryStoreCountable(cItem, amount, out var _);
            }

            if (!FindEmptySlot(0, out var found)) return false;
            return TryStoreInternal(modified, found.Index);
        }

        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            storedSlot = null;
            if (EnsureItemInstanceByType(item) is not { } modified
                || modified is ICountableItem) return false;
            if (!FindEmptySlot(0, out var found)) return false;

            int index = found.Index;
            bool result = TryStoreInternal(modified, index);
            storedSlot = result ? slots[index] : null;

            return result;
        }

        public bool TryStore(IGameItem item, int index)
        {
            if (EnsureItemInstanceByType(item) is not { } modified) return false;

            if (modified is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount <= 0) return false;

                return TryStoreCountable(cItem, amount, index, out var _);
            }

            return TryStoreInternal(modified, index);
        }

        // Quick check for store eligibility
        public bool CanStore(IGameItem item)
        {
            // Validate item/type and empty slot
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !inventoryValidItemTypes.Contains(itemInfo.itemType)
                || !FindEmptySlot(0, out _))
                return false;

            return true;
        }
        // Store eligibility for a specific slot
        public bool CanStore(IGameItem item, int index)
        {
            // Validate item and slot constraints
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !TryGetItemSlot(index, out var slot)
                || !slot.CanStore(itemInfo))
                return false;

            return true;
        }


        #endregion
    }
}

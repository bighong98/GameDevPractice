using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IDividableStorage

        public void TryDivide(int index, int expected)
        {
            Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) invoked", Logg.LoggingMode.Completed);
            if (GetSlot(index) is not
                {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable, GetAmount: {} total } item, // Cannot split if only 1
                } slot )
            {
                Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - not valid slot", Logg.LoggingMode.Completed);
                return;
            }

            if (item is not ICountableItem cItem)
                cItem = (ICountableItem)EnsureItemInstanceByType(item);

            int amount = Mathf.Min(expected, total - 1);
            if (amount <= 0) return; // Cannot split if only 1
            
            var clone = cItem.Clone<ICountableItem>(amount); // Clone with split amount
            // Try to store clone into empty slot
            if (!FindEmptySlot(0, out var emptySlot)
                || !TryStoreInternal(clone, emptySlot.Index))
            {
                Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - failed to store item", Logg.LoggingMode.InProgress);
                return;
            }
            // Apply remaining amount to source item
            Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - trying to SetAmount source item", Logg.LoggingMode.Completed);
            cItem.SetAmount(total - amount);
            NotifySlotChanged(slot);
        }

        #endregion
    }
}

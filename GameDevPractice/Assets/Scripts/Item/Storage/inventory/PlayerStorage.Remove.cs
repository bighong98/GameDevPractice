using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Remove (Take out)

        public bool TryRemoveItem(int index)
        {
            Logg.Log($"[PlayerStorage] TryRemove({index}) invoked", Logg.LoggingMode.InProgress);
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true, GetItem: {} item } slot) return false;
            
            if (item.Type == Enums.ItemType.Countable &&
                item.GetItemInfo is ItemTypeSO data &&
                item is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount > 0)
                    UpdateCountableDict(data, -amount);
            }

            CacheRemove(item, index);

            var result = slot.Clear();
            if (result) NotifySlotChanged(index);
            
            return result;
        }

        #endregion
    }
}


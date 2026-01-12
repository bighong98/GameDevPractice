using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public sealed class ConsumableStorageService : IConsumableStorageService
    {
        public delegate bool TryGetCountableAmountDelegate(ItemTypeSO itemData, out int total);
        public delegate bool TryGetCachedIndexDelegate(ItemTypeSO data, int minIndex, out int index);

        private readonly System.Func<int, bool> isValidSlotIdx;
        private readonly System.Func<int, IGameItemSlot> getSlot;
        private readonly System.Func<int> getEndIdx;
        private readonly TryGetCountableAmountDelegate tryGetCountableAmount;
        private readonly TryGetCachedIndexDelegate tryGetCachedIndex;
        private readonly System.Action<ItemTypeSO, int> updateCountableDict;
        private readonly System.Action<IGameItemSlot> notifySlotChanged;
        private readonly System.Action<ItemTypeSO, int> cacheAdd;
        private readonly System.Func<int, bool> tryRemoveItem;

        public ConsumableStorageService(
            System.Func<int, bool> isValidSlotIdx,
            System.Func<int, IGameItemSlot> getSlot,
            System.Func<int> getEndIdx,
            TryGetCountableAmountDelegate tryGetCountableAmount,
            TryGetCachedIndexDelegate tryGetCachedIndex,
            System.Action<ItemTypeSO, int> updateCountableDict,
            System.Action<IGameItemSlot> notifySlotChanged,
            System.Action<ItemTypeSO, int> cacheAdd,
            System.Func<int, bool> tryRemoveItem)
        {
            this.isValidSlotIdx = isValidSlotIdx;
            this.getSlot = getSlot;
            this.getEndIdx = getEndIdx;
            this.tryGetCountableAmount = tryGetCountableAmount;
            this.tryGetCachedIndex = tryGetCachedIndex;
            this.updateCountableDict = updateCountableDict;
            this.notifySlotChanged = notifySlotChanged;
            this.cacheAdd = cacheAdd;
            this.tryRemoveItem = tryRemoveItem;
        }

        public bool TryConsume(IGameItemSlot slot, int amount)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item }) return false;
            if (item.GetItemInfo is not { } itemInfo || !itemInfo.IsNotNull() || !itemInfo.isUsable) return false;

            switch (item.Type)
            {
                case Enums.ItemType.Countable:
                    if (item is not ICountableItem cItem) return false;
                    int before = cItem.GetAmount;
                    int expected = before - amount;
                    if (!cItem.TrySetAmount(expected)) return false;
                    if (before - cItem.GetAmount is int consumed and > 0)
                        updateCountableDict(itemInfo, -consumed);
                    notifySlotChanged(slot);
                    break;
                case Enums.ItemType.Special:
                    break;
                default:
                    return tryRemoveItem(slot.Index);
            }

            return true;
        }

        public bool TryConsume(int index, int amount)
        {
            if (!isValidSlotIdx(index)) return false;
            return TryConsume(getSlot(index), amount);
        }

        public bool TryConsume(ItemTypeSO itemData, int amount)
        {
            if (itemData == null || amount <= 0) return false;
            if (!itemData.isUsable) return false;
            if (itemData.itemType != Enums.ItemType.Countable) return false;

            if (!tryGetCountableAmount(itemData, out var total) || total < amount)
                return false;

            int remaining = amount;
            int startIndex = 0;
            int end = getEndIdx();
            if (end < 0) return false;

            while (remaining > 0 && tryGetCachedIndex(itemData, startIndex, out int idx))
            {
                if (!TryConsumeFromIndex(idx, itemData, ref remaining))
                {
                    startIndex = idx + 1;
                    continue;
                }

                startIndex = idx + 1;
            }

            for (int i = startIndex; i <= end && remaining > 0; i++)
            {
                if (!TryConsumeFromIndex(i, itemData, ref remaining))
                    continue;

                cacheAdd(itemData, i);
            }

            return remaining <= 0;
        }

        private bool TryConsumeFromIndex(int index, ItemTypeSO itemData, ref int remaining)
        {
            if (!isValidSlotIdx(index))
                return false;

            var slot = getSlot(index);

            if (slot is not
                {
                    IsAccessible: true,
                    HasItem: true,
                    GetItem: ICountableItem cItem,
                    GetItemInfo: ItemTypeSO info
                })
                return false;

            if (info != itemData)
                return false;

            int stackAmount = cItem.GetAmount;
            int toUse = Mathf.Min(remaining, stackAmount);
            if (toUse <= 0)
                return false;

            if (!TryConsume(slot, toUse))
                return false;
            remaining -= toUse;

            return true;
        }
    }
}

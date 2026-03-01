using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Item.Storage
{
    public sealed class CountableStorageService : ICountableStorageService
    {
        public delegate bool FindEmptySlotDelegate(int start, out IGameItemSlot found);

        private readonly System.Func<int, bool> isValidSlotIdx;
        private readonly System.Func<int> getEndIdx;
        private readonly System.Func<int, IGameItemSlot> getSlot;
        private readonly FindEmptySlotDelegate findEmptySlot;
        private readonly System.Func<IGameItem, int, bool> tryStoreInternal;
        private readonly System.Action<int> notifySlotChanged;
        private readonly System.Action<IGameItemSlot> notifySlotChangedSlot;
        private readonly CountableAmountCache countableCache;
        private readonly ItemIndexCache itemIndexCache;

        public CountableStorageService(
            System.Func<int, bool> isValidSlotIdx,
            System.Func<int> getEndIdx,
            System.Func<int, IGameItemSlot> getSlot,
            FindEmptySlotDelegate findEmptySlot,
            System.Func<IGameItem, int, bool> tryStoreInternal,
            System.Action<int> notifySlotChanged,
            System.Action<IGameItemSlot> notifySlotChangedSlot,
            CountableAmountCache countableCache,
            ItemIndexCache itemIndexCache)
        {
            this.isValidSlotIdx = isValidSlotIdx;
            this.getEndIdx = getEndIdx;
            this.getSlot = getSlot;
            this.findEmptySlot = findEmptySlot;
            this.tryStoreInternal = tryStoreInternal;
            this.notifySlotChanged = notifySlotChanged;
            this.notifySlotChangedSlot = notifySlotChangedSlot;
            this.countableCache = countableCache;
            this.itemIndexCache = itemIndexCache;
        }

        public bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess)
        {
            if (countableItem == null || amount <= 0)
            {
                excess = 0;
                return false;
            }

            excess = amount;
            int requested = amount;

            int start = 0;
            while (excess > 0)
            {
                if (!FindIdenticalCountable(countableItem, start, out var foundSlot))
                    break;
                if (!TryAddCountable(countableItem, excess, foundSlot.Index, out excess))
                    break;
                start = foundSlot.Index + 1;
            }

            while (excess > 0)
            {
                if (!findEmptySlot(0, out var found))
                    break;
                int idx = found.Index;

                if (!TryStoreCountable(countableItem, excess, idx, out var localExcess))
                    break;

                if (localExcess == excess)
                    break;

                excess = localExcess;
            }

            int storedTotal = requested - excess;
            return storedTotal > 0;
        }

        public bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess)
        {
            excess = amount;

            if (countableItem == null || amount <= 0) return false;
            if (!isValidSlotIdx(index)) return false;

            var slot = getSlot(index);
            if (slot is not { IsAccessible: true }) return false;

            int stored = 0;

            if (slot is
                {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable } slotItem
                } && countableItem.IsEqual(slotItem, ItemComparerExtension.ItemCompareMode.CompareData)
                && slotItem is ICountableItem slotCountable)
            {
                int before = slotCountable.GetAmount;
                int overflow = slotCountable.AddAmount(amount);
                int after = slotCountable.GetAmount;

                stored = after - before;
                excess = overflow;

                if (stored > 0)
                    notifySlotChanged(index);
            }
            else if (slot is { HasItem: false })
            {
                int maxStack = countableItem.GetItemInfo.maxAmount > 0
                    ? countableItem.GetItemInfo.maxAmount
                    : int.MaxValue;

                int put = Mathf.Min(amount, maxStack);
                var clone = countableItem.Clone<ICountableItem>(put);
                if (clone == null) return false;

                if (!tryStoreInternal((IGameItem)clone, index))
                    return false;

                stored = put;
                excess = amount - put;
            }
            else
            {
                return false;
            }

            if (stored > 0 && countableItem.GetItemInfo is { } data)
                countableCache.ApplyDelta(data, stored);

            return stored > 0;
        }

        public bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess)
        {
            if (countableItem == null || countableItem.IsEmpty || amount <= 0)
            {
                excess = amount;
                return false;
            }

            excess = amount;

            if (getSlot(index) is
                { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } slotItem } slot
                && countableItem.IsEqual(slotItem, ItemComparerExtension.ItemCompareMode.CompareData)
                && slotItem is ICountableItem slotCountable)
            {
                int before = slotCountable.GetAmount;
                int overflow = slotCountable.AddAmount(amount);
                int after = slotCountable.GetAmount;

                int stored = after - before;
                excess = overflow;

                if (stored > 0)
                {
                    if (countableItem.GetItemInfo is { } data)
                        countableCache.ApplyDelta(data, stored);

                    notifySlotChanged(index);
                }

                return stored > 0;
            }

            return false;
        }

        public bool TryGetCountableAmount(ICountableItem countableItem, out int amount)
        {
            if (countableItem.GetItemInfo is not { } itemInfo)
            {
                amount = 0;
                return false;
            }

            return TryGetCountableAmount(itemInfo, out amount);
        }

        public bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount)
        {
            if (itemInfo == null || !itemInfo.IsNotNull())
            {
                amount = 0;
                return false;
            }

            return countableCache.TryGetAmount(itemInfo, out amount);
        }

        public bool TryMergeStacks(int fromIndex, int toIndex)
        {
            if (!isValidSlotIdx(fromIndex) || !isValidSlotIdx(toIndex)) return false;
            if (fromIndex == toIndex) return false;

            if (getSlot(fromIndex) is not
                { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } fromItem } fromSlot)
                return false;

            if (getSlot(toIndex) is not
                { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } toItem } toSlot)
                return false;

            if (!fromItem.IsEqual(toItem, ItemComparerExtension.ItemCompareMode.CompareData))
                return false;

            var fromCount = ((ICountableItem)fromItem).GetAmount;
            var toCount = ((ICountableItem)toItem).GetAmount;

            if (fromCount <= 0) return false;

            int maxStack = toItem.GetItemInfo.maxAmount > 0
                ? toItem.GetItemInfo.maxAmount
                : int.MaxValue;

            int total = fromCount + toCount;
            int newTo = Mathf.Min(total, maxStack);
            int remain = total - newTo;

            (toItem as ICountableItem)?.SetAmount(newTo);
            notifySlotChangedSlot(toSlot);

            if (remain <= 0)
            {
                fromSlot.Clear();
                notifySlotChangedSlot(fromSlot);
            }
            else
            {
                ((ICountableItem)fromItem).SetAmount(remain);
                notifySlotChangedSlot(fromSlot);
            }

            return true;
        }

        public void RebuildCountableCache()
        {
            countableCache.Clear();
            int end = getEndIdx();
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                if (getSlot(i) is not
                    {
                        IsAccessible: true, HasItem: true,
                        GetItem: { Type: Enums.ItemType.Countable, GetItemInfo: ItemTypeSO data } item
                    })
                    continue;

                int amount = item.GetAmount;
                if (amount > 0)
                    countableCache.ApplyDelta(data, amount);
            }
        }

        private bool FindIdenticalCountable(ICountableItem cItem, int start, out IGameItemSlot slot)
        {
            slot = null;
            if (!isValidSlotIdx(start)) return false;

            if (cItem?.GetItemInfo is ItemTypeSO data &&
                itemIndexCache.TryGetCachedSlot(data, start, out var cachedSlot))
            {
                slot = cachedSlot;
                return true;
            }

            int end = getEndIdx();

            for (int i = start; i <= end; i++)
            {
                if (!IsIdenticalCountableItem(cItem, i, out var found)) continue;
                slot = found;

                if (found is { HasItem: true, GetItemInfo: ItemTypeSO foundData })
                    itemIndexCache.Add(foundData, i);

                return true;
            }

            return false;
        }

        public bool IsIdenticalCountableItem(IGameItem cItem, int index, out IGameItemSlot slot)
        {
            if (getSlot(index) is
                {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable } targetItem
                } targetSlot && cItem.IsEqual(targetItem, ItemComparerExtension.ItemCompareMode.CompareData))
            {
                slot = targetSlot;
                return true;
            }

            slot = null;
            return false;
        }
    }
}

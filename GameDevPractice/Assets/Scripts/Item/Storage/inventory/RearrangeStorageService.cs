using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Item
{
    public sealed class RearrangeStorageService : IRearrangeableStorageService
    {
        public delegate bool FindEmptySlotDelegate(int start, out IGameItemSlot found);

        private readonly Func<int, bool> isValidSlotIdx;
        private readonly Func<int, IGameItemSlot> getSlot;
        private readonly Func<int> getEndIdx;
        private readonly Func<int> getCapacity;
        private readonly FindEmptySlotDelegate findEmptySlot;
        private readonly Func<int, bool> tryRemoveItem;
        private readonly Action<IGameItem, int> cacheAdd;
        private readonly Action<IGameItem, int> cacheRemove;
        private readonly Action<int> notifySlotChanged;
        private readonly Action rebuildItemIndexCache;
        private readonly ICountableStorageService countableService;

        public RearrangeStorageService(
            Func<int, bool> isValidSlotIdx,
            Func<int, IGameItemSlot> getSlot,
            Func<int> getEndIdx,
            Func<int> getCapacity,
            FindEmptySlotDelegate findEmptySlot,
            Func<int, bool> tryRemoveItem,
            Action<IGameItem, int> cacheAdd,
            Action<IGameItem, int> cacheRemove,
            Action<int> notifySlotChanged,
            Action rebuildItemIndexCache,
            ICountableStorageService countableService)
        {
            this.isValidSlotIdx = isValidSlotIdx;
            this.getSlot = getSlot;
            this.getEndIdx = getEndIdx;
            this.getCapacity = getCapacity;
            this.findEmptySlot = findEmptySlot;
            this.tryRemoveItem = tryRemoveItem;
            this.cacheAdd = cacheAdd;
            this.cacheRemove = cacheRemove;
            this.notifySlotChanged = notifySlotChanged;
            this.rebuildItemIndexCache = rebuildItemIndexCache;
            this.countableService = countableService;
        }

        public bool TryTransferItem(IGameItemSlot fromSlot, IGameItemSlot toSlot)
        {
            if (fromSlot is not { IsAccessible: true, HasItem: true, GetItem: { } fromItem, Index: { } fromIndex }
                || toSlot is not { IsAccessible: true, Index: { } toIndex })
                return false;

            if (fromItem is ICountableItem fromCItem
                && toSlot.GetItem is ICountableItem toCItem
                && fromCItem.IsEqual(toCItem, ItemComparerExtension.ItemCompareMode.CompareData))
            {
                return countableService.TryMergeStacks(fromIndex, toIndex);
            }

            if (toSlot is { HasItem: true, GetItem: { } toItem })
                return SwapItem(fromSlot, fromItem, toSlot, toItem);

            return TransferItem(fromSlot, fromItem, toSlot);
        }

        public bool TryTransferItem(int fromIdx, int toIdx)
        {
            if (!isValidSlotIdx(fromIdx) || !isValidSlotIdx(toIdx)) return false;
            return TryTransferItem(getSlot(fromIdx), getSlot(toIdx));
        }

        public void Trim()
        {
            MergeStacks(false);
            if (!findEmptySlot(0, out var found)) return;
            int write = found.Index;
            int capacity = getCapacity();

            for (int read = write + 1; read < capacity; read++)
            {
                if (getSlot(read) is not { IsAccessible: true, HasItem: true }) continue;

                if (getSlot(write) is not { IsAccessible: true, HasItem: false })
                {
                    if (!findEmptySlot(write + 1, out var newFound))
                        break;
                    write = newFound.Index;
                }

                if (TryTransferItem(read, write))
                {
                    if (!findEmptySlot(write + 1, out var newFound))
                        break;
                    write = newFound.Index;
                }
            }
            rebuildItemIndexCache();
        }

        public void Sort()
        {
            MergeStacks(false);
            int capacity = getCapacity();
            var items = new List<(int index, IGameItem item)>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                if (getSlot(i) is { IsAccessible: true, HasItem: true, GetItem: { } it })
                    items.Add((i, it));
            }

            items.Sort(CompareItemsForSort);

            int targetCount = items.Count;

            for (int pos = 0; pos < targetCount; pos++)
            {
                var targetItem = items[pos].item;

                if (getSlot(pos) is { HasItem: true, GetItem: { } cur } && ReferenceEquals(cur, targetItem))
                    continue;

                int curIdx = -1;
                for (int i = pos; i < capacity; i++)
                {
                    if (getSlot(i) is { HasItem: true, GetItem: { } it } && ReferenceEquals(it, targetItem))
                    {
                        curIdx = i;
                        break;
                    }
                }
                if (curIdx < 0) continue;

                if (getSlot(pos) is { IsAccessible: true, HasItem: false })
                    TryTransferItem(curIdx, pos);
                else TryTransferItem(getSlot(curIdx), getSlot(pos));
            }

            for (int i = targetCount; i < capacity; i++)
            {
                if (getSlot(i) is { IsAccessible: true, HasItem: true })
                    tryRemoveItem(i);
            }
            rebuildItemIndexCache();
        }

        public void MergeStacks(bool trimAfter = false)
        {
            int end = getEndIdx();
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                if (getSlot(i) is not { IsAccessible: true, HasItem: true, GetItem: { } ti }) continue;
                if (ti.GetItemInfo.itemType != Enums.ItemType.Countable) continue;

                var target = (ICountableItem)ti;
                int maxStack = target.GetItemInfo.maxAmount > 0 ? target.GetItemInfo.maxAmount : int.MaxValue;

                int space = maxStack - target.GetAmount;
                if (space <= 0) continue;

                for (int j = i + 1; j <= end && space > 0; j++)
                {
                    if (!IsIdenticalCountableItem(ti, j, out var donorSlot)) continue;
                    if (donorSlot is not { HasItem: true, GetItem: IGameItem dj }) continue;

                    var donor = (ICountableItem)dj;
                    int donorAmt = donor.GetAmount;
                    if (donorAmt <= 0) continue;

                    int move = Mathf.Min(space, donorAmt);

                    int overflow = target.AddAmount(move);
                    int actuallyMoved = move - overflow;

                    if (actuallyMoved > 0)
                    {
                        donor.SetAmount(donorAmt - actuallyMoved);
                        space -= actuallyMoved;

                        if (donor.GetAmount <= 0)
                            donorSlot.Clear();
                    }

                    if (overflow > 0)
                        donor.SetAmount(donor.GetAmount + overflow);
                }
            }

            if (trimAfter) Trim();
        }

        private bool TransferItem(IGameItemSlot fromSlot, IGameItem fromItem, IGameItemSlot toSlot)
        {
            if (toSlot.TryStore(fromItem) && fromSlot.Clear())
            {
                int fromIndex = fromSlot.Index;
                int toIndex = toSlot.Index;
                cacheRemove(fromItem, fromIndex);
                cacheAdd(fromItem, toIndex);
                notifySlotChanged(fromIndex);
                notifySlotChanged(toIndex);
                return true;
            }

            fromSlot.TryStore(fromItem, byForce: true);
            toSlot.Clear(byForce: true);
            return false;
        }

        private bool SwapItem(IGameItemSlot fromSlot, IGameItem fromItem, IGameItemSlot toSlot, IGameItem toItem)
        {
            if (toSlot.TryStore(fromItem) && fromSlot.TryStore(toItem))
            {
                int fromIndex = fromSlot.Index;
                int toIndex = toSlot.Index;

                cacheRemove(fromItem, fromIndex);
                cacheRemove(toItem, toIndex);

                cacheAdd(fromItem, toIndex);
                cacheAdd(toItem, fromIndex);

                notifySlotChanged(fromIndex);
                notifySlotChanged(toIndex);
                return true;
            }

            fromSlot.TryStore(fromItem, byForce: true);
            toSlot.TryStore(toItem, byForce: true);
            return false;
        }

        private static int CompareItemsForSort((int index, IGameItem item) a, (int index, IGameItem item) b)
        {
            var ai = a.item.GetItemInfo;
            var bi = b.item.GetItemInfo;

            int typeCompare = ai.itemType.CompareTo(bi.itemType);
            if (typeCompare != 0) return typeCompare;

            string an = ai.nameString ?? string.Empty;
            string bn = bi.nameString ?? string.Empty;
            int nameCompare = StringComparer.Ordinal.Compare(an, bn);
            if (nameCompare != 0) return nameCompare;

            int ac = (a.item is ICountableItem aci) ? aci.GetAmount : 0;
            int bc = (b.item is ICountableItem bci) ? bci.GetAmount : 0;

            return bc.CompareTo(ac);
        }

        private bool IsIdenticalCountableItem(IGameItem cItem, int index, out IGameItemSlot slot)
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

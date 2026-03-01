using System;
using System.Collections.Generic;
using TH.Resource;

namespace TH.Item.Storage
{
    public sealed class ItemIndexCache
    {
        private readonly Dictionary<ItemTypeSO, List<int>> itemIndexCache = new();
        private readonly Func<int, bool> isValidSlotIdx;
        private readonly Func<int, IGameItemSlot> getSlot;
        private readonly Func<int> getEndIdx;

        public ItemIndexCache(
            Func<int, bool> isValidSlotIdx,
            Func<int, IGameItemSlot> getSlot,
            Func<int> getEndIdx)
        {
            this.isValidSlotIdx = isValidSlotIdx;
            this.getSlot = getSlot;
            this.getEndIdx = getEndIdx;
        }

        public void Clear() => itemIndexCache.Clear();

        public void Add(ItemTypeSO itemInfo, int index)
        {
            if (!isValidSlotIdx(index)) return;
            if (!itemIndexCache.TryGetValue(itemInfo, out var list))
            {
                list = new List<int>();
                itemIndexCache[itemInfo] = list;
            }

            if (!list.Contains(index))
                list.Add(index);
        }

        public void Add(IGameItem item, int index)
        {
            if (item?.GetItemInfo is not ItemTypeSO data) return;
            Add(data, index);
        }

        public void Remove(IGameItem item, int index)
        {
            if (item?.GetItemInfo is not ItemTypeSO data) return;
            if (!itemIndexCache.TryGetValue(data, out var list)) return;

            list.Remove(index);
            if (list.Count == 0)
                itemIndexCache.Remove(data);
        }

        public void ClearSlot(IGameItemSlot slot)
        {
            if (slot is { HasItem: true, GetItem: { } item })
                Remove(item, slot.Index);
        }

        public bool TryGetCachedIndex(ItemTypeSO data, int minIndex, out int index)
        {
            index = -1;
            if (data == null) return false;
            if (!itemIndexCache.TryGetValue(data, out var list)) return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                int idx = list[i];

                if (idx < minIndex) continue;

                if (!isValidSlotIdx(idx))
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (getSlot(idx) is { HasItem: true, GetItemInfo: ItemTypeSO slotData } && slotData == data)
                {
                    index = idx;
                    return true;
                }

                list.RemoveAt(i);
            }

            if (list.Count == 0)
                itemIndexCache.Remove(data);

            return false;
        }

        public bool TryGetCachedSlot(ItemTypeSO data, int minIndex, out IGameItemSlot slot)
        {
            slot = null;
            if (data == null) return false;
            if (!itemIndexCache.TryGetValue(data, out var list)) return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                int idx = list[i];

                if (idx < minIndex) continue;

                if (!isValidSlotIdx(idx))
                {
                    list.RemoveAt(i);
                    continue;
                }

                var cachedSlot = getSlot(idx);
                if (cachedSlot is { IsAccessible: true, HasItem: true, GetItem: { } item } &&
                    item.GetItemInfo is ItemTypeSO slotData &&
                    slotData == data)
                {
                    slot = cachedSlot;
                    return true;
                }

                list.RemoveAt(i);
            }

            if (list.Count == 0)
                itemIndexCache.Remove(data);

            return false;
        }

        public void Rebuild()
        {
            itemIndexCache.Clear();

            int end = getEndIdx();
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                if (getSlot(i) is { IsAccessible: true, HasItem: true, GetItem: { } item })
                {
                    Add(item, i);
                }
            }
        }
    }
}

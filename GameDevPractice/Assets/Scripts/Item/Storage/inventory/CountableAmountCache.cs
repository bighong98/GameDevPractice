using System;
using System.Collections.Generic;
using TH.Resource;

namespace TH.Item
{
    public sealed class CountableAmountCache
    {
        private readonly Dictionary<ItemTypeSO, int> amounts = new();
        private readonly Action<ItemTypeSO, int> onChanged;

        public CountableAmountCache(Action<ItemTypeSO, int> onChanged)
        {
            this.onChanged = onChanged;
        }

        public void Clear() => amounts.Clear();

        public bool TryGetAmount(ItemTypeSO itemInfo, out int amount)
            => amounts.TryGetValue(itemInfo, out amount);

        public void ApplyDelta(ItemTypeSO data, int delta)
        {
            if (data == null || delta == 0) return;

            if (!amounts.TryGetValue(data, out var current))
                current = 0;

            current += delta;
            if (current <= 0) amounts.Remove(data);
            else amounts[data] = current;

            onChanged?.Invoke(data, current);
        }
    }
}

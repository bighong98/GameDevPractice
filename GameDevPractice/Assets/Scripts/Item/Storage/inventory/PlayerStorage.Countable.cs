using System;
using TH.Resource;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region ICountableItemStorage

        public event Action<ItemTypeSO, int> OnCountableAmountModified;

        public bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess)
            => countableService.TryStoreCountable(countableItem, amount, out excess);

        public bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess)
            => countableService.TryStoreCountable(countableItem, amount, index, out excess);

        public bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess)
            => countableService.TryAddCountable(countableItem, amount, index, out excess);

        public bool TryGetCountableAmount(ICountableItem countableItem, out int amount)
            => countableService.TryGetCountableAmount(countableItem, out amount);

        public bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount)
            => countableService.TryGetCountableAmount(itemInfo, out amount);

        public bool TryMergeStacks(int fromIndex, int toIndex)
            => countableService.TryMergeStacks(fromIndex, toIndex);

        private void RebuildCountableCache()
        {
            countableService.RebuildCountableCache();
        }

        private void UpdateCountableDict(ItemTypeSO data, int delta)
        {
            countableCache.ApplyDelta(data, delta);
        }

        private void RaiseCountableAmountModified(ItemTypeSO data, int amount)
        {
            OnCountableAmountModified?.Invoke(data, amount);
        }

        #endregion
    }
}

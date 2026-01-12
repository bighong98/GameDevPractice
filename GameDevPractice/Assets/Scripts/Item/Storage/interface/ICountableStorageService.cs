using TH.Resource;

namespace TH.Item
{
    public interface ICountableStorageService
    {
        bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess);
        bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess);
        bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess);
        bool TryMergeStacks(int fromIndex, int toIndex);
        bool TryGetCountableAmount(ICountableItem countableItem, out int amount);
        bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount);
        void RebuildCountableCache();
        bool IsIdenticalCountableItem(IGameItem cItem, int index, out IGameItemSlot slot);
    }
}

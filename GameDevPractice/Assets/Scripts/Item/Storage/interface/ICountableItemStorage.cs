using UnityEngine;

namespace TH.Item
{
    public interface ICountableItemStorage
    {
        bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess);
        bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess);
        bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess);
        bool TryMergeStacks(int fromIndex, int toIndex);
    }
}


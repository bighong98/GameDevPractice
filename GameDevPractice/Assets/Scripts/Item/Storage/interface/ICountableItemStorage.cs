using UnityEngine;

namespace TH.Item
{
    public interface ICountableItemStorage
    {
        bool TryStore(ICountableItem countableItem, int amount, out int excess);
        bool TryAdd(ICountableItem countableItem, int amount, int index, out int excess);
    }
}


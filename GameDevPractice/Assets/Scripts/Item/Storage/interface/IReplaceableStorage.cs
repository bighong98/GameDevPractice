using UnityEngine;

namespace TH.Item
{
    public interface IReplaceableStorage
    {
        bool TryStore(IGameItem item, out IGameItem existing);
        bool TryStore(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing);
        bool TryStore(IGameItem item, int index, out IGameItem existing);
    }
}


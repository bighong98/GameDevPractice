using UnityEngine;

namespace TH.Item.Storage
{
    public interface IIntraSwappableStorage
    {
        bool TrySwap(IGameItemSlot one, IGameItemSlot another);
    }
}


using UnityEngine;

namespace TH.Item
{
    public interface IItemUsageHandler
    {
        void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot);
        public void Consume(IGameItemStorage source, object destination, IGameItemSlot slot, int amount = 1);
    }
}


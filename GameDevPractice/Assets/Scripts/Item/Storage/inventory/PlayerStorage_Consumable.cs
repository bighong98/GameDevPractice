using TH.Resource;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IConsumableItemStorage

        public bool TryConsume(IGameItemSlot slot, int amount)
            => consumableService.TryConsume(slot, amount);

        public bool TryConsume(int index, int amount)
            => consumableService.TryConsume(index, amount);

        public bool TryConsume(ItemTypeSO itemData, int amount)
            => consumableService.TryConsume(itemData, amount);

        #endregion
    }
}

using TH.Resource;

namespace TH.Item
{
    public interface IConsumableStorageService
    {
        bool TryConsume(IGameItemSlot slot, int amount);
        bool TryConsume(int index, int amount);
        bool TryConsume(ItemTypeSO itemData, int amount);
    }
}

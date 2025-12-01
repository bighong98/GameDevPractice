
namespace TH.Item
{
    public readonly struct ItemUseContext
    {
        public readonly object User;
        public readonly IGameItem Item;
        public readonly IGameItemStorage Storage;
        public readonly IGameItemSlot Slot;
        public readonly int Amount;

        public ItemUseContext(
            object user,
            IGameItem item,
            IGameItemStorage storage,
            IGameItemSlot slot,
            int amount)
        {
            User    = user;
            Item    = item;
            Storage = storage;
            Slot    = slot;
            Amount  = amount;
        }
    }
}


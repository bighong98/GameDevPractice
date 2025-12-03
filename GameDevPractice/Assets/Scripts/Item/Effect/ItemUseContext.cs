
using TH.Resource;

namespace TH.Item
{
    public readonly struct ItemUseContext
    {
        public readonly object User;
        public readonly ItemTypeSO ItemInfo;
        public readonly int Amount;

        public ItemUseContext(
            object user,
            ItemTypeSO itemInfo,
            int amount)
        {
            User = user;
            ItemInfo = itemInfo;
            Amount = amount;
        }
    }
}


using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    public sealed class ReplaceStorageService : IReplaceableStorageService
    {
        public delegate bool FindEmptySlotDelegate(int start, out IGameItemSlot found);
        public delegate bool TryGetItemSlotDelegate(int index, out IGameItemSlot slot);

        private readonly System.Func<int, bool> isValidSlotIdx;
        private readonly System.Func<int, IGameItemSlot> getSlot;
        private readonly FindEmptySlotDelegate findEmptySlot;
        private readonly TryGetItemSlotDelegate tryGetItemSlot;
        private readonly System.Func<IGameItem, int, bool> tryStoreAt;
        private readonly System.Action<ItemTypeSO, int> updateCountableDict;
        private readonly System.Action<IGameItem, int> cacheRemove;
        private readonly System.Action<int> notifySlotChanged;

        public ReplaceStorageService(
            System.Func<int, bool> isValidSlotIdx,
            System.Func<int, IGameItemSlot> getSlot,
            FindEmptySlotDelegate findEmptySlot,
            TryGetItemSlotDelegate tryGetItemSlot,
            System.Func<IGameItem, int, bool> tryStoreAt,
            System.Action<ItemTypeSO, int> updateCountableDict,
            System.Action<IGameItem, int> cacheRemove,
            System.Action<int> notifySlotChanged)
        {
            this.isValidSlotIdx = isValidSlotIdx;
            this.getSlot = getSlot;
            this.findEmptySlot = findEmptySlot;
            this.tryGetItemSlot = tryGetItemSlot;
            this.tryStoreAt = tryStoreAt;
            this.updateCountableDict = updateCountableDict;
            this.cacheRemove = cacheRemove;
            this.notifySlotChanged = notifySlotChanged;
        }

        public bool TryReplace(IGameItem item, out IGameItem existing)
        {
            return TryReplace(item, out _, out existing);
        }

        public bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing)
        {
            if (!findEmptySlot(0, out storedSlot))
            {
                existing = null;
                return false;
            }

            return TryReplaceAt(item, storedSlot.Index, out existing);
        }

        public bool TryReplaceAt(IGameItem item, int index, out IGameItem existing)
        {
            existing = null;
            if (!tryGetItemSlot(index, out var slot) || !slot.IsAccessible)
                return false;

            return (!slot.HasItem || TryTakeOut(index, out existing)) && tryStoreAt(item, index);
        }

        public bool TryTakeOut(int index, out IGameItem item)
        {
            Logg.Log($"[PlayerStorage] TryTakeOut({index}) invoked", Logg.LoggingMode.Completed);
            item = default;
            if (!isValidSlotIdx(index)) return false;
            if (getSlot(index) is not { IsAccessible: true, HasItem: true } slot) return false;
            if (!slot.Clear(out var stored)) return false;

            item = stored;

            if (item.Type == Enums.ItemType.Countable &&
                item is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount > 0)
                    updateCountableDict(cItem.GetItemInfo, -amount);
            }

            cacheRemove(item, index);
            notifySlotChanged(index);
            return true;
        }
    }
}

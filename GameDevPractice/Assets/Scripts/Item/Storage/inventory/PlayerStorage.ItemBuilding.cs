using TH.Item.Storage;
using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Item/Slot instance building

        private void FillInventoryWithEmptySlots()
        {
            if (slots.Count > 0)
            {
                Logg.Log($"[{nameof(PlayerStorage)}] item slot list has something before initialization", Logg.LoggingMode.Completed);
                slots.Clear();
            }
            for (int i = 0; i < InitialCapacity; i++)
            {
                slots.Add(MakeEmptySlot(i));
            }
        }

        private IGameItemSlot MakeEmptySlot(int index)
        {
            return new ItemSlot(index: index, item: null, validTypes: inventoryValidItemTypes);
        }

        private readonly IItemBuilder itemBuilder = new ItemBuilder();
        private IPlayerStorage _playerStorageImplementation;

        private IGameItem EnsureItemInstanceByType(IGameItem item)
        {
            if (item is not { GetAmount: int amount and > 0, GetItemInfo: { } itemInfo }) return null;
            return itemBuilder.GetItemFromData(itemInfo, amount);
        }
        private IGameItem EnsureItemInstanceByType(ItemTypeSO data, int amount = 1)
        {
            return itemBuilder.GetItemFromData(data, amount);
        }

        #endregion
    }
}


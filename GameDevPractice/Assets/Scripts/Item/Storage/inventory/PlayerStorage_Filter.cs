namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IFilterableStorage

        public void SetFilter(InventoryFilterType filter)
        {
            if (CurrentFilter == filter) return;
            CurrentFilter = filter;

            for (int i = 0; i < capacity; i++)
            {
                var slot = slots[i];
                slot.SetVisibility(IsVisibleByFilter(slot, filter));
            }
            
            OnFilterChanged?.Invoke(CurrentFilter);
        }

        
        public static bool IsVisibleByFilter(IGameItemSlot slot, InventoryFilterType filter)
        {
            return filter switch
            {
                InventoryFilterType.All => true,
                InventoryFilterType.Equipment => slot is { GetItemInfo: { itemType: Enums.ItemType.Equipment } },
                InventoryFilterType.Consumable => slot is { GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: true }, GetAmount: > 0 }, 
                InventoryFilterType.Resource => slot is { GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: false }, GetAmount: > 0 }, 
                _ => false
            };
        }



        #endregion
    }
}

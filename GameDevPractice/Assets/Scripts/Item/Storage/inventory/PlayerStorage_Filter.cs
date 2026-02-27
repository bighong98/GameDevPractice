namespace TH.Item
{
    // 인벤토리 필터 상태 관리 partial
    public sealed partial class PlayerStorage
    {
        #region IFilterableStorage

        // 필터 상태 갱신 및 슬롯 가시성 재평가
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

        
        // 필터 타입별 슬롯 표시 조건 판별
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

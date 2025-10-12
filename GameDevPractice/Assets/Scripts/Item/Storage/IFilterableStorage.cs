using System;

namespace TH.Item
{
    public interface IFilterableStorage
    {
        event Action<InventoryFilterType> OnInventoryFilterChanged;
        public InventoryFilterType CurrFilter { get; }
        public void SetFilter(InventoryFilterType filter);
    }

}

using System;

namespace TH.Item
{
    public interface IFilterableStorage
    {
        event Action<InventoryFilterType> OnFilterChanged;
        public InventoryFilterType CurrentFilter { get; }
        public void SetFilter(InventoryFilterType filter);
    }

}

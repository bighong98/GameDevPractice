
using InventorySystem = RPG.Item.InventorySystem;
using System;

namespace TH.Item
{
    public interface IFilterableStorage
    {
        event Action<InventorySystem.InventoryFilterType> OnInventoryFilterChanged;
        public InventorySystem.InventoryFilterType CurrFilter { get; }
    }

}

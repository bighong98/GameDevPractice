namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Type Validation
        
        private readonly Enums.ItemType[] inventoryValidItemTypes = // Allowed item types for inventory
        {
            Enums.ItemType.Countable,
            Enums.ItemType.Special,
            Enums.ItemType.Equipment,
            Enums.ItemType.Single,
        };

        #endregion
    }
}



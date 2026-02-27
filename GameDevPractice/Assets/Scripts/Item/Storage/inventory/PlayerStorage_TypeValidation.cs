namespace TH.Item
{
    // 인벤토리 허용 아이템 타입 규칙 partial
    public sealed partial class PlayerStorage
    {
        #region Type Validation
        
        // 인벤토리 슬롯 허용 타입 목록
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



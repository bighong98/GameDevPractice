using UnityEngine;

namespace TH.Item
{
    public interface IGameItem
    {
        ItemTypeSO GetItemInfo { get; }
        Enums.ItemType Type { get; }
        int GetAmount { get; }
        bool IsValid { get; }
        bool IsEmpty { get; }

        T Clone<T>();
    }
}



using UnityEngine;

namespace TH.Item
{
    public interface IItemBuilder
    {
        IGameItem GetItemFromData(ItemTypeSO data, int amount = 1);
    }
}


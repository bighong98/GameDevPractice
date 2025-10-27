using UnityEngine;

namespace TH.Item
{
    public static class ItemComparerExtension
    {
        public enum ItemCompareMode
        {
            CompareData,
            CompareInstance,
        }

        public static bool IsEqual(this IGameItem source, IGameItem other, ItemCompareMode mode)
        {
            switch (mode)
            {
                case ItemCompareMode.CompareData:
                    if (source is not { GetAmount: > 0, GetItemInfo: { } sData }) return false;
                    if (other is not { GetAmount: > 0, GetItemInfo: { } oData }) return false;
                    return sData == oData;
                case ItemCompareMode.CompareInstance:
                    return source == other;
                default:
                    break;
            }
            return false;
        }
    }
}


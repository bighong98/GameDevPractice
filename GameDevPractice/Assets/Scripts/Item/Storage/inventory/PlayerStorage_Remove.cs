using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    // 아이템 제거 규칙 partial
    public sealed partial class PlayerStorage
    {
        #region Remove (Take out)

        // 슬롯 인덱스 기반 아이템 제거 진입 메서드
        public bool TryRemoveItem(int index)
        {
            Logg.Log($"[PlayerStorage] TryRemove({index}) invoked", Logg.LoggingMode.InProgress);
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true, GetItem: {} item } slot) return false;
            
            if (item.Type == Enums.ItemType.Countable &&
                item.GetItemInfo is ItemTypeSO data &&
                item is ICountableItem cItem)
            {
                // 수량형 캐시 합계 차감 반영 분기
                int amount = cItem.GetAmount;
                if (amount > 0)
                    UpdateCountableDict(data, -amount);
            }

            CacheRemove(item, index);

            var result = slot.Clear();
            if (result) NotifySlotChanged(index);
            
            return result;
        }

        #endregion
    }
}


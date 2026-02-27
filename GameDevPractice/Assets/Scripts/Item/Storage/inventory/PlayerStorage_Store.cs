using System.Linq;

namespace TH.Item
{
    // 아이템 저장 가능성 검증 및 저장 진입 partial
    public sealed partial class PlayerStorage
    {
        #region Store

        // 지정 슬롯 즉시 저장 내부 루틴
        private bool TryStoreInternal(IGameItem item, int index)
        {
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: false } slot) return false;

            var result = slot.TryStore(item);
            if (result) 
            {
                CacheAdd(item, index);
                NotifySlotChanged(index);
            }
            return result;
        } 

        // 자동 슬롯 탐색 기반 저장 진입 메서드
        public bool TryStore(IGameItem item)
        {
            if (EnsureItemInstanceByType(item) is not { } modified) return false;

            if (modified is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount <= 0) return false;

                return TryStoreCountable(cItem, amount, out var _);
            }

            if (!FindEmptySlot(0, out var found)) return false;
            return TryStoreInternal(modified, found.Index);
        }

        // 자동 슬롯 탐색 기반 저장 후 슬롯 반환
        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            storedSlot = null;
            if (EnsureItemInstanceByType(item) is not { } modified
                || modified is ICountableItem) return false;
            if (!FindEmptySlot(0, out var found)) return false;

            int index = found.Index;
            bool result = TryStoreInternal(modified, index);
            storedSlot = result ? slots[index] : null;

            return result;
        }

        // 지정 슬롯 기반 저장 진입 메서드
        public bool TryStore(IGameItem item, int index)
        {
            if (EnsureItemInstanceByType(item) is not { } modified) return false;

            if (modified is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount <= 0) return false;

                return TryStoreCountable(cItem, amount, index, out var _);
            }

            return TryStoreInternal(modified, index);
        }

        // Quick check for store eligibility
        // 저장 가능 여부 빠른 검증
        public bool CanStore(IGameItem item)
        {
            // Validate item/type and empty slot
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !inventoryValidItemTypes.Contains(itemInfo.itemType)
                || !FindEmptySlot(0, out _))
                return false;

            return true;
        }
        // Store eligibility for a specific slot
        // 특정 슬롯 저장 가능 여부 빠른 검증
        public bool CanStore(IGameItem item, int index)
        {
            // Validate item and slot constraints
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !TryGetItemSlot(index, out var slot)
                || !slot.CanStore(itemInfo))
                return false;

            return true;
        }


        #endregion
    }
}

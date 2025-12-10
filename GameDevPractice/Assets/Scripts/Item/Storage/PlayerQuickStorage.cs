using System;
using System.Collections.Generic;
using System.Linq;
using TH.Utils;
using UnityEngine;

namespace TH.Item.Storage
{
    
    // 플레이어 퀵슬롯 목록 관리 (슬롯 수 QuickStorageCapacity: 5)
    // 퀵 슬롯 내부 아이템은 단순 ItemTypeSO 참조 보관용 더미 인스턴스 (GameItemTransfer 등 아이템 이동 관련 제약 추가 필요)
    // 실제 아이템 개수 관리, 사용 처리는 PlayerStorage에서 처리
    
    // 추후 기능 확장을 고려한 인터페이스 래퍼
    public interface IQuickStorage : IGameItemStorage, IReferenceStorage { }
    public class PlayerQuickStorage : IQuickStorage
    {
        public IReadOnlyCollection<IGameItemSlot> ItemSlots 
            => readonlySlots ??= slots.AsReadOnly();

        private readonly List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: QuickStorageCapacity);
        private IReadOnlyCollection<IGameItemSlot> readonlySlots;

        public int Capacity => QuickStorageCapacity;

        public event Action<IGameItemSlot> OnSlotChanged;
        public event Action OnStorageChanged;

        private const int QuickStorageCapacity = 5;

        // 퀵슬롯에 허용할 아이템 타입.
        private static readonly Enums.ItemType[] quickSlotValidItemTypes =
        {
            Enums.ItemType.Countable,
            Enums.ItemType.Single,
        };

        public PlayerQuickStorage()
        {
            InitSlots();
        }

        #region Initialization

        private void InitSlots()
        {
            if (slots.Count > 0) return;

            for (int i = 0; i < QuickStorageCapacity; i++)
            {
                slots.Add(MakeEmptySlot(i));
            }
        }

        private IGameItemSlot MakeEmptySlot(int index)
        {
            return new ItemSlot(index: index, item: null, validTypes: quickSlotValidItemTypes);
        }

        #endregion

        #region Get(Item / Slot)

        public bool TryGetItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) &&
                slots[index] is { IsAccessible: true, HasItem: true, GetItem: { } slotItem } )
            {
                item = slotItem;
                return true;
            }

            item = default;
            return false;
        }

        public bool TryGetItemSlot(int index, out IGameItemSlot itemSlot)
        {
            if (IsValidSlotIdx(index) &&
                slots[index] is { IsAccessible: true } slot)
            {
                itemSlot = slot;
                return true;
            }

            itemSlot = default;
            return false;
        }

        #endregion

        #region Remove

        public bool TryRemoveItem(int index)
        {
            if (!IsValidSlotIdx(index)) return false;

            if (slots[index] is not { IsAccessible: true, HasItem: true } slot)
                return false;

            // 슬롯 인스턴스는 유지하고 내부 아이템만 제거
            bool result = slot.Clear(out var _);
            if (result)
            {
                OnSlotChanged?.Invoke(slot);
                OnStorageChanged?.Invoke();
            }

            return result;
        }

        #endregion

        #region Store

        // 빈 퀵슬롯에 아이템을 저장 (좌측부터 순서대로 빈 슬롯에 등록 -> 빈 슬롯 없으면 실패)
        // 퀵 스토리지의 아이템은 단순 아이템 데이터 식별용 래퍼임에 주의 (ItemTransfer 등으로 이동x) //todo: 아이템 반출 제약 추가
        public bool TryStore(IGameItem item)
        {
            if (item == null) return false;

            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] is not { IsAccessible: true, HasItem: false } slot) return false;
                
                bool result = TryStore(item, i);
                if (result) return true;
            }

            return false;
        }

        
        // 빈 퀵슬롯에 아이템을 저장하고, 저장된 슬롯을 반환.
        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            storedSlot = default;
            if (item == null) return false;

            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] is not { IsAccessible: true, HasItem: false } slot) return false;
                
                bool result = TryStore(item, i);
                if (!result) continue;
                
                storedSlot = slots[i];
                return true;
            }

            return false;
        }

        // 빈 퀵슬롯에 아이템을 저장 (좌측부터 순서대로 빈 슬롯에 등록 -> 빈 슬롯 없으면 실패)
        // 퀵 스토리지의 아이템은 단순 아이템 데이터 식별용 래퍼임에 주의 (ItemTransfer 등으로 이동x) //todo: 아이템 반출 제약 추가
        public bool TryStore(IGameItem item, int index)
        {
            // 등록 아이템 유효성 검사
            if (item is not {GetItemInfo: {} itemInfo} 
                || !itemInfo.IsAlive() 
                || !itemInfo.isUsable) return false;
            // 등록 슬롯 유효성 검사
            if (!IsValidSlotIdx(index)) 
                return false;
            if (slots[index] is not { IsAccessible: true } slot)
                return false;
            
            IGameItem previousItem = null;
            bool clearEquivalentItem = false;
            
            // 1. 동일한 데이터의 아이템이 이미 저장되어 있는지 확인
            // -> 다른 슬롯에 동일 아이템이 있다면 해당 슬롯 비우기
            // -> 필요시 롤백을 대비해 해당 슬롯 정보, 슬롯 비우기 성공 여부 캐싱 (previousItem, equivalentItemSlot, clearEquivalentItem)
            if (ContainsEquivalent(item, out var equivalentItemSlot))
            {
                clearEquivalentItem = 
                    TryGetItem(equivalentItemSlot.Index, out previousItem)
                    && TryRemoveItem(equivalentItemSlot.Index);
            }

            // 2. 새로운 슬롯에 아이템 등록 시도
            if (!slot.TryStore(item))
            {
                // 등록 실패 시 기존 슬롯 비우기 절차가 있었다면 롤백 시도
                if (clearEquivalentItem
                    && !equivalentItemSlot.TryStore(previousItem))
                {
                    Logg.LogWarning($"[{GetType().Name}.TryStore] Failed to rollback while storing item at slot({index})." 
                       + "Previous item at slot({equivalentItemSlot.Index}) may be lost.");
                }
                // 롤백 성공 여부와 무관하게 false 반환
                return false;
            }
            
            // 3. 아이템 등록 성공 시 이벤트 호출, true 반환
            OnSlotChanged?.Invoke(slot);
            OnStorageChanged?.Invoke();
            return true;
        }

        public bool CanStore(IGameItem item)
        {
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !quickSlotValidItemTypes.Contains(itemInfo.itemType)
                || !itemInfo.isUsable)
                return false;
        
            return true;
        }

        public bool CanStore(IGameItem item, int index)
        {
            // 퀵슬롯은 새 아이템 등록 시 덮어쓰기되는 것을 의도했기에 별도의 인덱스 검사 없이 아이템 타입만 검사함
            // 퀵슬롯 별로 저장 제약이 추가된다면 수정 필요
            return CanStore(item);
        }

        #endregion

        #region Helper

        private bool IsValidSlotIdx(int index)
        {
            return index >= 0 && index < Mathf.Min(slots.Count, Capacity);
        }

        private bool ContainsEquivalent(IGameItem newItem, out IGameItemSlot equivalentItemSlot)
        {
            equivalentItemSlot = null;
            if (newItem is not { IsEmpty: false, GetItemInfo: {} itemInfo } || !itemInfo.IsAlive())
            {
                Logg.LogWarning($"[{GetType().Name}.ContainsEquivalent] Invalid item instance entered");
                return false;
            }

            foreach (var slot in slots)
            {
                if (slot is { HasItem: true, GetItem: {} slotItem }
                    && slotItem.IsEqual(newItem, ItemComparerExtension.ItemCompareMode.CompareData))
                {
                    equivalentItemSlot = slot;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region IReferenceStorage

        public bool TryStoreReference(IGameItem item)
        {
            return TryStore(item);
        }

        public bool TryStoreReference(IGameItem item, int index)
        {
            return TryStore(item, index);
        }

        public bool TryRemoveReference(int index)
        {
            return TryRemoveItem(index);
        }

        #endregion
    }
}

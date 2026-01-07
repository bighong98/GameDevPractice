using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using TH.Resource;
using TH.Item.Storage;
using System.Linq;
using UnityEngine.SceneManagement;


namespace TH.Item
{
    public sealed class PlayerStorage : IPlayerStorage, ISavableEntity
    {
        public event Action<IGameItemSlot> OnSlotChanged; //직접 .Invoke() 호출하지 말고 NotifySlotChanged(index) 사용할 것
        public event Action OnStorageChanged;
        public event Action<IGameItemSlot> OnItemTryUsed; 
        public event Action<int> OnCapacityChanged;
        public event Action<InventoryFilterType> OnFilterChanged;

        public InventoryFilterType CurrentFilter { get; private set; } = InventoryFilterType.All;

        public IReadOnlyCollection<IGameItemSlot> ItemSlots => slots;
        private List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: maxCapacity);
        private readonly Dictionary<ItemTypeSO, int> countableDict = new(); // CountableItem의 종류별 개수 (trim, sort 최적화 목적)

        public int Capacity => capacity;
        private int capacity;
        public int MaxCapacity => maxCapacity;
        private const int maxCapacity = 256;
        private const int InitialCapacity = 80;
        
        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        // public PlayerStorage(IResourceLoader resourceLoader, ISaveSystem saveSystem)
        public PlayerStorage(IResourceLoader resourceLoader, ISaveEntityRegistry saveEntityRegistry)
        {
            Init();

            resourceLoader.OnLabelResourcesLoadedAll += (label) =>
            {
                if (!string.Equals(label, Constants.PreLoadLabel)) return;
                saveEntityRegistry.RegisterEntity(this);
                LoadTestData(resourceLoader);
            };
        }
        
        #region Initialization

        private void Init()
        {
            SetCapacity(InitialCapacity);
            Clear();
        }

        private const string InventoryTestDataSOKey = "InventoryTestDataSO";
        private bool isTestDataLoaded = false;
        private bool _hasRestoredState = false;

        
        private void LoadTestData(IResourceLoader resourceLoader)
        {
            if (isTestDataLoaded || _hasRestoredState) return;
            
            if (!resourceLoader.TryLoad<InventoryTestDataSO>(InventoryTestDataSOKey, out var testData))
            {
                Logg.LogError("TestData is null");
                return;
            }

            foreach (var (itemReference, amount) in testData.Items)
            {
                if (!resourceLoader.TryLoad<ItemTypeSO>(itemReference, out var item))
                {
                    Logg.LogError($"[{GetType().Name} - LoadTestData] Trying to load item from ({itemReference}, {amount})");
                    continue;
                }
                Logg.Log($"Trying to add ({item.nameString}, {amount})", Logg.LoggingMode.Completed);
                if (!TryStore(EnsureItemInstanceByType(item, amount)))
                {
                    Logg.LogError($"[PlayerInventory] failed to add test data item ({item.nameString}, {amount})");
                }
            }

            isTestDataLoaded = true;
        }
        
        #endregion
        
        #region Store

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

        // 아이템 저장 가능 여부 확인 메서드
        public bool CanStore(IGameItem item)
        {
            // 아이템 유효성 검사, 아이템 타입 유효성 검사, 빈 슬롯 존재 여부 확인
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !inventoryValidItemTypes.Contains(itemInfo.itemType)
                || !FindEmptySlot(0, out _))
                return false;

            return true;
        }
        // 아이템 저장 가능 여부 확인 메서드 (인덱스로 슬롯 특정)
        public bool CanStore(IGameItem item, int index)
        {
            // 아이템 유효성 검사, 인덱스 슬롯에 저장 가능 여부 확인
            if (item is not { IsValid: true, GetItemInfo: {} itemInfo }
                || !TryGetItemSlot(index, out var slot)
                || !slot.CanStore(itemInfo))
                return false;

            return true;
        }


        #endregion

        #region IReplaceableStorage

        public bool TryReplace(IGameItem item, out IGameItem existing)
        {
            return TryReplace(item, out _, out existing);
        }

        public bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing)
        {
            if (!FindEmptySlot(0, out storedSlot))
            {
                existing = null;
                return false;
            }
            
            return TryReplaceAt(item, storedSlot.Index, out existing);
        }

        public bool TryReplaceAt(IGameItem item, int index, out IGameItem existing)
        {
            existing = null;
            if (!TryGetItemSlot(index, out var slot) || !slot.IsAccessible)
                return false;

            return (!slot.HasItem || TryTakeOut(index, out existing)) && TryStore(item, index);
        }

        public bool TryTakeOut(int index, out IGameItem item)
        {
            Logg.Log($"[PlayerStorage] TryTakeOut({index}) invoked", Logg.LoggingMode.Completed);
            item = default;
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true } slot) return false;
            if (!slot.Clear(out var stored)) return false;

            item = stored;

            if (item.Type == Enums.ItemType.Countable &&
                item is ICountableItem cItem)
            {
                int amount = cItem.GetAmount;
                if (amount > 0)
                    UpdateCountableDict(cItem.GetItemInfo, -amount);
            }

            CacheRemove(item, index);
            NotifySlotChanged(index);
            return true;
        }

        #endregion

        #region IUsableItemStorage
        
        public bool TryStoreAndUse(IGameItem item, object user = null)
        {
            if (!TryStore(item, out var storedSlot)) return false;
            
            this.Log($"TryStoreAndUse({item}) - Store succeed. call OnItemTryUsed.Invoke({storedSlot})", Logg.LoggingMode.Completed);
            OnItemTryUsed?.Invoke(storedSlot);
            return true;
        }

        public bool TryStoreAndUse(IGameItem item, int index, object user = null)
        {
            if (!TryStore(item, index)) return false;
            
            OnItemTryUsed?.Invoke(slots[index]);
            return true;
        }
        
        #endregion

        #region IConsumableItemStorage

        public bool TryConsume(IGameItemSlot slot, int amount)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: {} item }) return false;
            if (item.GetItemInfo is not {} itemInfo || !itemInfo.IsNotNull() || !itemInfo.isUsable ) return false;

            switch (item.Type)
            {
                case Enums.ItemType.Countable:
                    if (item is not ICountableItem cItem) return false;
                    int before = cItem.GetAmount;
                    int expected = before - amount;
                    if (!cItem.TrySetAmount(expected)) return false;
                    // int consumed = before - cItem.GetAmount;
                    // if (consumed > 0)
                    //     UpdateCountableDict(itemInfo, -consumed);
                    if (before - cItem.GetAmount is int consumed and > 0)
                        UpdateCountableDict(itemInfo, -consumed);
                    NotifySlotChanged(slot);
                    break;
                case Enums.ItemType.Special:
                    break;
                default: return TryRemoveItem(slot.Index);
            }
            
            return true;
        }

        public bool TryConsume(int index, int amount)
        {
            if (!IsValidSlotIdx(index)) return false;
            return TryConsume(slots[index], amount);
        }

        public bool TryConsume(ItemTypeSO itemData, int amount)
        {
            // 타입/조건 검증
            if (itemData == null || amount <= 0) return false;
            if (!itemData.isUsable) return false;
            if (itemData.itemType != Enums.ItemType.Countable) return false;

            // 1) 전체 보유량 빠른 체크 (부족하면 바로 실패)
            if (!countableDict.TryGetValue(itemData, out var total) || total < amount)
                return false;

            int remaining = amount;
            int startIndex = 0;
            int end = GetEndIdx;

            // 2) 캐시에 기록된 슬롯들부터 소비
            while (remaining > 0 && TryGetCachedIndex(itemData, startIndex, out int idx))
            {
                if (!TryConsumeFromIndex(idx, itemData, ref remaining))
                {
                    // 이 슬롯에서 소비를 못 했으면 그냥 다음 슬롯으로 넘김
                    startIndex = idx + 1;
                    continue;
                }

                startIndex = idx + 1;
            }

            // 3) 캐시에서 더 이상 못 찾았으면, 남은 영역 전체 스캔
            for (int i = startIndex; i <= end && remaining > 0; i++)
            {
                if (!TryConsumeFromIndex(i, itemData, ref remaining))
                    continue;

                // 캐시에 없던 슬롯이면 등록
                CacheAdd(itemData, i);
            }

            // 요청한 양을 전부 소비했을 때만 true
            return remaining <= 0;
        }

        private bool TryConsumeFromIndex(int index, ItemTypeSO itemData, ref int remaining)
        {
            if (!IsValidSlotIdx(index)) 
                return false;

            var slot = slots[index];

            // 이 슬롯이 소비 가능한 Countable이고, itemData와 타입이 같은지 확인
            if (slot is not
                {
                    IsAccessible: true,
                    HasItem: true,
                    GetItem: ICountableItem cItem,
                    GetItemInfo: ItemTypeSO info
                })
                return false;

            if (info != itemData) 
                return false;

            // 슬롯에서 소비 가능한 최대치 계산
            int stackAmount = cItem.GetAmount;
            int toUse = Mathf.Min(remaining, stackAmount);
            if (toUse <= 0) 
                return false;

            // 해당 슬롯에서 소비 시도
            if (!TryConsume(slot, toUse)) 
                return false;
            remaining -= toUse;

            return true;
        }

        #endregion

        #region ICountableItemStorage

        public event Action<ItemTypeSO, int> OnCountableAmountModified;

        public bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess)
        {
            if (countableItem == null || amount <= 0)
            {
                excess = 0;
                return false;
            }

            excess = amount;
            int requested = amount;

            // 1) 동일 스택에 병합 (가득 찰 때까지)
            int start = 0;
            while (excess > 0)
            {
                if (!FindIdenticalCountable(countableItem, start, out var foundSlot))
                    break;
                if (!TryAddCountable(countableItem, excess, foundSlot.Index, out excess))
                    break;
                start = foundSlot.Index + 1;
            }

            // 2) 남은 양을 빈 슬롯에 신규 스택으로 채우기
            while (excess > 0)
            {
                if (!FindEmptySlot(0, out var found))
                    break;
                int idx = found.Index;

                // 빈 슬롯에 amount 만큼 넣기 -> 실패할 경우 루프 종료
                if (!TryStoreCountable(countableItem, excess, idx, out var localExcess))
                    break;

                // 아무것도 저장 못 했으면 루프 종료
                if (localExcess == excess) 
                    break;

                excess = localExcess;
            }

            // dict 갱신은 TryAdd / TryStoreCountable 내부에서 이미 처리됨
            int storedTotal = requested - excess;
            return storedTotal > 0;
        }

        public bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess)
        {
            excess = amount;

            if (countableItem == null || amount <= 0) return false;
            if (!IsValidSlotIdx(index)) return false;

            var slot = GetSlot(index);
            if (slot is not { IsAccessible: true }) return false;

            int stored = 0;

            // 1) 이미 동일 Countable 이 있는 슬롯인 경우 : 수량만 더하기
            if (slot is
                {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable } slotItem
                } && countableItem.IsEqual(slotItem, ItemComparerExtension.ItemCompareMode.CompareData)
                && slotItem is ICountableItem slotCountable)
            {
                int before = slotCountable.GetAmount;
                int overflow = slotCountable.AddAmount(amount); // max 넘어가면 overflow 반환
                int after = slotCountable.GetAmount;

                stored = after - before;
                excess = overflow;

                if (stored > 0)
                    NotifySlotChanged(index);
            }
            // 2) 슬롯이 비어있는 경우 : 새 스택으로 저장
            else if (slot is { HasItem: false })
            {
                int maxStack = countableItem.GetItemInfo.maxAmount > 0
                    ? countableItem.GetItemInfo.maxAmount
                    : int.MaxValue;

                int put = Mathf.Min(amount, maxStack);
                var clone = countableItem.Clone<ICountableItem>(put);
                if (clone == null) return false;

                if (!TryStoreInternal((IGameItem)clone, index))
                    return false;

                stored = put;
                excess = amount - put;
            }
            else // 다른 타입 아이템이 차 있는 슬롯이면 저장 불가
            {
                return false;
            }

            if (stored > 0 && countableItem.GetItemInfo is {} data)
                UpdateCountableDict(data, stored); 

            return stored > 0;
        }

        public bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess)
        {
            if (countableItem == null || countableItem.IsEmpty || amount <= 0)
            {
                excess = amount;
                return false;
            }

            excess = amount;

            if (GetSlot(index) is
                    { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } slotItem } slot
                && countableItem.IsEqual(slotItem, ItemComparerExtension.ItemCompareMode.CompareData)
                && slotItem is ICountableItem slotCountable)
            {
                int before = slotCountable.GetAmount;
                int overflow = slotCountable.AddAmount(amount);
                int after = slotCountable.GetAmount;

                int stored = after - before;
                excess = overflow;

                if (stored > 0)
                {
                    if (countableItem.GetItemInfo is {} data)
                        UpdateCountableDict(data, stored); 

                    NotifySlotChanged(index);
                }

                return stored > 0;
            }

            return false;
        }
        // 특정 Countable 아이템의 개수 확인 (저장소 내에 존재하지 않는 아이템이라면 false 반환)
        public bool TryGetCountableAmount(ICountableItem countableItem, out int amount)
        {
            if (countableItem.GetItemInfo is not {} itemInfo)
            {
                amount = 0;
                return false;
            }

            return TryGetCountableAmount(itemInfo, out amount);
        }
        
        public bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount)
        {
            if (itemInfo == null || !itemInfo.IsNotNull())
            {
                amount = 0;
                return false;
            }
            return countableDict.TryGetValue(itemInfo, out amount);
        }

        private void UpdateCountableDict(ItemTypeSO data, int delta)
        {
            if (data == null || delta == 0) return;

            if (!countableDict.TryGetValue(data, out var current))
                current = 0;

            // 개수 변동 반영
            current += delta;
            if (current <= 0) countableDict.Remove(data);
            else countableDict[data] = current;
            // 변동 이벤트 전파
            OnCountableAmountModified?.Invoke(data, current);
        }

        public bool TryMergeStacks(int fromIndex, int toIndex)
        {
            // 인덱스 유효성 + 자기 자신 병합 방지
            if (!IsValidSlotIdx(fromIndex) || !IsValidSlotIdx(toIndex)) return false;
            if (fromIndex == toIndex) return false;

            if (GetSlot(fromIndex) is not
                { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } fromItem } fromSlot)
                return false;

            if (GetSlot(toIndex) is not
                { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } toItem } toSlot)
                return false;

            // 동일 데이터(동일 ItemTypeSO 등)인지 검사
            if (!fromItem.IsEqual(toItem, ItemComparerExtension.ItemCompareMode.CompareData))
                return false;

            var fromCount = ((ICountableItem)fromItem).GetAmount;
            var toCount   = ((ICountableItem)toItem).GetAmount;

            if (fromCount <= 0) return false;

            int maxStack = toItem.GetItemInfo.maxAmount > 0
                ? toItem.GetItemInfo.maxAmount
                : int.MaxValue;

            int total  = fromCount + toCount;
            int newTo  = Mathf.Min(total, maxStack);
            int remain = total - newTo;

            // to 슬롯에 가능한 만큼 채워넣기
            (toItem as ICountableItem)?.SetAmount(newTo);
            NotifySlotChanged(toSlot);

            if (remain <= 0)
            {
                // from 슬롯은 비워버림
                fromSlot.Clear();
                NotifySlotChanged(fromSlot);
            }
            else
            {
                // 남은 수량만 from 슬롯에 유지
                ((ICountableItem)fromItem).SetAmount(remain);
                NotifySlotChanged(fromSlot);
            }

            // 여기서는 "총합"이 바뀌지 않기 때문에 countableDict는 건드리지 않음.
            return true;
        }


        private void RebuildCountableCache()
        {
            countableDict.Clear();
            int end = GetEndIdx;
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                if (slots[i] is not
                    { IsAccessible: true, HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable, GetItemInfo: ItemTypeSO data } item })
                    continue;

                int amount = item.GetAmount;
                if (amount > 0)
                    UpdateCountableDict(data, amount);
            }
        }
    
        #endregion

        #region IDividableStorage

        public void TryDivide(int index, int expected)
        {
            Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) invoked", Logg.LoggingMode.Completed);
            if (GetSlot(index) is not
                {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable, GetAmount: {} total } item, // 1개는 분리 불가
                } slot )
            {
                Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - not valid slot", Logg.LoggingMode.Completed);
                return;
            }

            if (item is not ICountableItem cItem)
                cItem = (ICountableItem)EnsureItemInstanceByType(item);

            int amount = Mathf.Min(expected, total - 1);
            if (amount <= 0) return; // 1개는 분리 불가
            
            var clone = cItem.Clone<ICountableItem>(amount); // 아이템의 복사본 생성 + 분리한 개수 주입
            // 빈 슬롯 탐색 + 해당 슬롯에 복사본 저장 시도
            if (!FindEmptySlot(0, out var emptySlot)
                || !TryStoreInternal(clone, emptySlot.Index))
            {
                Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - failed to store item", Logg.LoggingMode.InProgress);
                return;
            }
            // 복사본 분리 저장 성공 -> 기존 아이템에 개수 반영 및 인벤토리 변동 이벤트 전달
            Logg.Log($"[PlayerStorage] TryDivide({index}, {expected}) - trying to SetAmount source item", Logg.LoggingMode.Completed);
            cItem.SetAmount(total - amount);
            NotifySlotChanged(slot);
        }

        #endregion
        
        #region Get (Find)

        public bool TryGetItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) 
                && slots[index] is {IsAccessible: true, HasItem: true, GetItem: {} slotItem })
            {
                item = slotItem;
                return true;
            }
            
            item = default;
            return false;
        }

        public bool TryGetItemSlot(int index, out IGameItemSlot itemSlot)
        {
            if (IsValidSlotIdx(index) && slots[index] is { IsAccessible: true } slot)
            {
                itemSlot = slot;
                return true;
            }

            itemSlot = default;
            return false;
        }

        #endregion

        #region Remove (Take out)

        public bool TryRemoveItem(int index)
        {
            Logg.Log($"[PlayerStorage] TryRemove({index}) invoked", Logg.LoggingMode.InProgress);
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true, GetItem: {} item } slot) return false;
            
            if (item.Type == Enums.ItemType.Countable &&
                item.GetItemInfo is ItemTypeSO data &&
                item is ICountableItem cItem)
            {
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
        
        #region IRearrangeableStorage (Transfer/Swap/Trim/Sort)
        
        // 스토리지 내부 슬롯 간 아이템 이동
        // Transfer (or Swap)
        public bool TryTransferItem(IGameItemSlot fromSlot, IGameItemSlot toSlot)
        {
            if (fromSlot is not { IsAccessible: true, HasItem: true, GetItem: { } fromItem, Index: {} fromIndex } 
                || toSlot is not { IsAccessible: true, Index: {} toIndex } ) 
                return false; // 출발 슬롯과 도착 슬롯 중에 유효하지 않은 슬롯이 존재할 경우 실패

            if (fromItem is ICountableItem fromCItem
                && toSlot.GetItem is ICountableItem toCItem 
                && fromCItem.IsEqual(toCItem, ItemComparerExtension.ItemCompareMode.CompareData))
            {
                // 내부 재배치니까 PlayerStorage 전용 병합 로직 사용
                return TryMergeStacks(fromIndex, toIndex);
            }

            if (toSlot is { HasItem: true, GetItem: { } toItem }) // 도착 슬롯에 기존 아이템이 있는 경우 -> 아이템 자리 교체 (Swap)
                return SwapItem(fromSlot, fromItem, toSlot, toItem);
            
            return TransferItem(fromSlot, fromItem, toSlot); // ** 없는 경우 -> 단순 아이템 이동
        }

        public bool TryTransferItem(int fromIdx, int toIdx)
        {
            if (!IsValidSlotIdx(fromIdx) || !IsValidSlotIdx(toIdx)) return false;
            return TryTransferItem(slots[fromIdx], slots[toIdx]);
        }

        private bool TransferItem(IGameItemSlot fromSlot, IGameItem fromItem, IGameItemSlot toSlot)
        {
            if (toSlot.TryStore(fromItem) && fromSlot.Clear()) {
                
                int fromIndex = fromSlot.Index;
                int toIndex = toSlot.Index;
                // 아이템 위치 캐시 갱신
                CacheRemove(fromItem, fromIndex);
                CacheAdd(fromItem, toIndex);
                // 아이템 변동 이벤트 호출
                NotifySlotChanged(fromIndex);
                NotifySlotChanged(toIndex);
                return true; // 도착 슬롯에 아이템 저장 + 출발 슬롯 아이템 제거
            }

            // 저장 실패 or 출발 슬롯 정리 실패 시 원복
            fromSlot.TryStore(fromItem, byForce: true); // 출발 슬롯에 원래 아이템을 강제 저장
            toSlot.Clear(byForce: true); // 도착 슬롯을 강제 정리
            return false;
        }

        private bool SwapItem(IGameItemSlot fromSlot, IGameItem fromItem, IGameItemSlot toSlot, IGameItem toItem)
        {
            if (toSlot.TryStore(fromItem) && fromSlot.TryStore(toItem)) {
                int fromIndex = fromSlot.Index;
                int toIndex   = toSlot.Index;
                
                CacheRemove(fromItem, fromIndex);
                CacheRemove(toItem, toIndex);

                CacheAdd(fromItem, toIndex);
                CacheAdd(toItem, fromIndex);

                NotifySlotChanged(fromIndex);
                NotifySlotChanged(toIndex);
                return true; // 아이템 슬롯 간 아이템 교환 시도
            }

            // 교환 실패 시 원복
            fromSlot.TryStore(fromItem, byForce: true);
            toSlot.TryStore(toItem, byForce: true);
            return false;
        }
        
        // Trim
        public void Trim()
        {
            MergeStacks(false);
            // 1) 가장 앞쪽 빈 슬롯 인덱스부터 시작
            if (!FindEmptySlot(0, out var found)) return;
            int write = found.Index;

            // 2) write 이후에서 아이템을 찾아 한 칸씩 앞으로 당김
            for (int read = write + 1; read < capacity; read++)
            {
                if (slots[read] is not { IsAccessible: true, HasItem: true }) continue;

                // 2-a) slots[write]가 접근불가능or빈칸x -> 다음 빈슬롯 탐색
                if (slots[write] is not { IsAccessible: true, HasItem: false })
                {
                    if (!FindEmptySlot(write+1, out var newFound))
                        break;
                    write = newFound.Index;
                }

                // 2-b) 빈 슬롯으로 아이템 이동
                if (TryTransferItem(read, write))
                {
                    if (!FindEmptySlot(write+1, out var newFound))
                        break;
                    write = newFound.Index;
                }
            }
            // 3) 아이템 인덱스 캐시 리빌드
            RebuildItemIndexCache();
        }
        
        // Sort

        public void Sort()
        {
            MergeStacks(false);
            // 1) 현재 아이템 참조 수집 (앞부분만 사용)
            var items = new List<(int index, IGameItem item)>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                if (slots[i] is { IsAccessible: true, HasItem: true, GetItem: { } it })
                    items.Add((i, it));
            }
            
            // 2) 정렬 규칙에 의거하여 정렬 순서 확정
            items.Sort(CompareItemsForSort);

            // 3) 앞에서부터 목표 순서대로 배치
            int targetCount = items.Count;

            for (int pos = 0; pos < targetCount; pos++)
            {
                var targetItem = items[pos].item;

                // 이미 제자리에 있으면 스킵
                if (slots[pos] is { HasItem: true, GetItem: { } cur } && ReferenceEquals(cur, targetItem))
                    continue;

                // 현재 targetItem이 있는 위치를 찾음
                int curIdx = -1;
                for (int i = pos; i < capacity; i++)
                {
                    if (slots[i] is { HasItem: true, GetItem: { } it } && ReferenceEquals(it, targetItem))
                    {
                        curIdx = i;
                        break;
                    }
                }
                if (curIdx < 0) continue; // 방어

                // pos가 비어있으면 단순 이동, 차있으면 Swap
                if (slots[pos] is { IsAccessible: true, HasItem: false })
                    TryTransferItem(curIdx, pos);
                else TryTransferItem(slots[curIdx], slots[pos]);
            }

            // 4) 나머지 뒤쪽은 비우기 (병합 없이 깔끔히 뒤를 비워줌)
            for (int i = targetCount; i < capacity; i++)
            {
                if (slots[i] is { IsAccessible: true, HasItem: true })
                    TryRemoveItem(i);
            }

            RebuildItemIndexCache();
        }
        
        private static int CompareItemsForSort((int index, IGameItem item) a, (int index, IGameItem item) b)
        {
            var ai = a.item.GetItemInfo;
            var bi = b.item.GetItemInfo;

            // 1) ItemType 우선
            int typeCompare = ai.itemType.CompareTo(bi.itemType);
            if (typeCompare != 0) return typeCompare;

            // 2) 이름 (Null-safe, Ordinal)
            string an = ai.nameString ?? string.Empty;
            string bn = bi.nameString ?? string.Empty;
            int nameCompare = StringComparer.Ordinal.Compare(an, bn);
            if (nameCompare != 0) return nameCompare;

            // 3) Countable이면 수량 내림차순 (없으면 0)
            int ac = (a.item is ICountableItem aci) ? aci.GetAmount : 0;
            int bc = (b.item is ICountableItem bci) ? bci.GetAmount : 0;

            return bc.CompareTo(ac);
        }
        
        // Merge Countables
        
        public void MergeStacks(bool trimAfter = false)
        {
            int end = GetEndIdx;
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                // 타깃: 접근 가능 + 아이템 보유 + Countable
                if (slots[i] is not { IsAccessible: true, HasItem: true, GetItem: {} ti }) continue;
                if (ti.GetItemInfo.itemType != Enums.ItemType.Countable) continue;

                var target = (ICountableItem)ti;
                int maxStack = target.GetItemInfo.maxAmount > 0 ? target.GetItemInfo.maxAmount : int.MaxValue;

                // 이미 가득차면 패스
                int space = maxStack - target.GetAmount;
                if (space <= 0) continue;

                // 뒤쪽 동일 스택들에서 끌어오기
                for (int j = i + 1; j <= end && space > 0; j++)
                {
                    if (!IsIdenticalCountableItem(ti, j, out var donorSlot)) continue;
                    if (donorSlot is not { HasItem: true, GetItem: IGameItem dj }) continue;

                    var donor = (ICountableItem)dj;
                    int donorAmt = donor.GetAmount;
                    if (donorAmt <= 0) continue;

                    int move = Mathf.Min(space, donorAmt);

                    // 먼저 타깃에 더해보기
                    int overflow = target.AddAmount(move);   // 구현상 넘어가면 overflow 반환
                    int actuallyMoved = move - overflow;

                    if (actuallyMoved > 0)
                    {
                        donor.SetAmount(donorAmt - actuallyMoved);
                        space -= actuallyMoved;

                        // 기증자가 0되면 슬롯 비우기
                        if (donor.GetAmount <= 0)
                            donorSlot.Clear(); // 개별 Notify 안 함 (나중에 컨트롤러에서 일괄 갱신)
                    }

                    // overflow가 생겼다면 기증자에게 되돌려주기
                    if (overflow > 0)
                        donor.SetAmount(donor.GetAmount + overflow);
                }
            }

            if (trimAfter) Trim();
            // RebuildItemIndexCache(); //todo: rebuild 시점 고려
        }

        
        #endregion
        
        #region Slot Helper Methods

        private void NotifySlotChanged(int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (slots[index] is not { } slot) return;
            
            NotifySlotChanged(slot);
        }

        private void NotifySlotChanged(IGameItemSlot slot)
        {
            Logg.Log($"[PlayerStorage] NotifySlotChanged({slot} - {slot.Index})", Logg.LoggingMode.Completed);
            slot.SetVisibility(IsVisibleByFilter(slot, CurrentFilter));
            OnSlotChanged?.Invoke(slot);
        }

        private IGameItemSlot GetSlot(int index)
        {
            if (!IsValidSlotIdx(index)) return null;
            return slots[index];
        }

        private bool FindEmptySlot(int start, out IGameItemSlot found)
        {
            int end = GetEndIdx;
            for (int i = start; i <= end; i++)
            {
                switch (slots[i])
                {
                    case null:
                        found = slots[i] = MakeEmptySlot(index: i); // 해당 인덱스에 최초 접근시, ItemSlot 인스턴스 생성
                        return true;
                    case { IsAccessible: true, HasItem: false }:
                        found = slots[i];
                        return true;
                }
            }

            found = null;
            return false;
        }

        private bool FindIdenticalCountable(ICountableItem cItem, int start, out IGameItemSlot slot)
        {
            slot = null;
            if (!IsValidSlotIdx(start)) return false;

            // 0) 캐시 먼저 확인 (lazy clean 포함)
            if (cItem?.GetItemInfo is ItemTypeSO data &&
                itemIndexCache.TryGetValue(data, out var cachedList))
            {
                for (int i = cachedList.Count - 1; i >= 0; i--)
                {
                    int idx = cachedList[i];

                    // 시작 인덱스보다 앞이면 스킵 (캐시에서 지우지는 않음)
                    if (idx < start) continue;

                    // 인덱스 자체가 유효하지 않으면 캐시에서 제거
                    if (!IsValidSlotIdx(idx))
                    {
                        cachedList.RemoveAt(i);
                        continue;
                    }

                    var s = slots[idx];
                    if (s is { IsAccessible: true, HasItem: true, GetItem: { } item } &&
                        item.GetItemInfo is ItemTypeSO slotData &&
                        slotData == data)
                    {
                        slot = s;
                        return true; 
                    }

                    // 내용이 바뀐 경우 캐시에서 제거
                    cachedList.RemoveAt(i);
                }

                if (cachedList.Count == 0)
                    itemIndexCache.Remove(data);
            }

            // 1) 캐시에서 못 찾았으면 기존 버전 그대로
            int end = GetEndIdx;

            for (int i = start; i <= end; i++)
            {
                if (!IsIdenticalCountableItem(cItem, i, out var found)) continue;
                slot = found; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환

                // 찾은 결과를 캐시에 기록 (다음 호출 최적화)
                if (found is { HasItem: true, GetItemInfo: ItemTypeSO foundData })
                {
                    CacheAdd(foundData, i);
                }

                return true;
            }

            return false;
        }


        private bool IsIdenticalCountableItem(IGameItem cItem, int index, out IGameItemSlot slot)
        {
            if (GetSlot(index) is {
                    HasItem: true,
                    GetItem: { Type: Enums.ItemType.Countable } targetItem
                } targetSlot && cItem.IsEqual(targetItem, ItemComparerExtension.ItemCompareMode.CompareData))
            {
                slot = targetSlot;
                return true;
            }

            slot = null;
            return false;
        }
        
        public bool SetCapacity(int capa, bool byForce = false)
        {
            if (capa > maxCapacity || capa == capacity) return false; // 현재 capacity와 동일하거나 최대 capacity를 초과하면 false

            if (capa > capacity) ExpandCapacity(capa);
            else ShrinkCapacity(capa);
            
            capacity = capa;
            OnCapacityChanged?.Invoke(capa);
            return true;
        }

        private void ShrinkCapacity(int capa)
        {
            for (int i = capa; i < capacity; i++)
            {
                if (GetSlot(i) is not { } slot) continue;
                slot.SetVisibility(false);
                slot.SetAccessibility(false);
            }
        }

        private void ExpandCapacity(int capa)
        {
            for (int i = capacity; i < capa; i++)
            {
                if (GetSlot(i) is not { } slot)
                {
                    var newSlot = MakeEmptySlot(i);
                    slots.Add(newSlot);
                    slot = newSlot;
                }
                
                slot.SetVisibility(true);
                slot.SetAccessibility(true);
            }
        }

        #endregion

        #region ISavable (save/load)

        private const string InventoryIdentifier = "playerInventory";
        public string UniqueIdentifier => InventoryIdentifier;
        public bool IsGlobal { get; } = true;
        public bool IsRegistered {get; set;} = false;
        public Scene TargetScene { get; } = default;

        public object CaptureState()
        {
            this.Log($"CaptureState", Logg.LoggingMode.Completed);
            List<IGameItem> items = new();

            foreach (var slot in slots)
            {
                if (slot is not { HasItem: true, GetItem: { } item }) continue;
                items.Add(item.Clone<IGameItem>());
            }

            return items;
        }

        public bool RestoreState(object state)
        {
            _hasRestoredState = true;

            this.Log($"RestoreState", Logg.LoggingMode.Completed);

            Clear();
            
            List<IGameItem> items = ExtractSaveData(state);
            foreach (var item in items)
            {
                TryStore(itemBuilder.GetItemFromData(item.GetItemInfo, item.GetAmount));
            }
            
            OnStorageChanged?.Invoke();
            
            return true;
        }

        private static List<IGameItem> ExtractSaveData(object state)
        {
            switch (state)
            {
                case List<IGameItem> l: return l;
                case Dictionary<string, object> stateDict:
                {
                    foreach (var s in stateDict.Values)
                        if (s is List<IGameItem> { } dl)
                            return dl;
                    break;
                }
            }
            return null;
        }

        #endregion
        
        #region Type Validation
        
        private readonly Enums.ItemType[] inventoryValidItemTypes = // 인벤토리 슬롯: 모든 아이템 가능
        {
            Enums.ItemType.Countable,
            Enums.ItemType.Special,
            Enums.ItemType.Equipment,
            Enums.ItemType.Single,
        };

        #endregion

        #region Item/Slot instance building

        private void FillInventoryWithEmptySlots()
        {
            if (slots.Count > 0)
            {
                Logg.Log($"[{nameof(PlayerStorage)}] item slot list has something before initialization", Logg.LoggingMode.Completed);
                slots.Clear();
            }
            for (int i = 0; i < InitialCapacity; i++)
            {
                slots.Add(MakeEmptySlot(i));
            }
        }

        private IGameItemSlot MakeEmptySlot(int index)
        {
            return new ItemSlot(index: index, item: null, validTypes: inventoryValidItemTypes);
        }

        private readonly IItemBuilder itemBuilder = new ItemBuilder();
        private IPlayerStorage _playerStorageImplementation;

        private IGameItem EnsureItemInstanceByType(IGameItem item)
        {
            if (item is not { GetAmount: int amount and > 0, GetItemInfo: { } itemInfo }) return null;
            return itemBuilder.GetItemFromData(itemInfo, amount);
        }
        private IGameItem EnsureItemInstanceByType(ItemTypeSO data, int amount = 1)
        {
            return itemBuilder.GetItemFromData(data, amount);
        }

        #endregion

        #region IFilterableStorage

        public void SetFilter(InventoryFilterType filter)
        {
            if (CurrentFilter == filter) return;
            CurrentFilter = filter;

            for (int i = 0; i < capacity; i++)
            {
                var slot = slots[i];
                slot.SetVisibility(IsVisibleByFilter(slot, filter));
            }
            
            OnFilterChanged?.Invoke(CurrentFilter);
        }

        
        public static bool IsVisibleByFilter(IGameItemSlot slot, InventoryFilterType filter)
        {
            return filter switch
            {
                InventoryFilterType.All => true,
                InventoryFilterType.Equipment => slot is { GetItemInfo: { itemType: Enums.ItemType.Equipment } },
                InventoryFilterType.Consumable => slot is { GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: true }, GetAmount: > 0 }, 
                InventoryFilterType.Resource => slot is { GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: false }, GetAmount: > 0 }, 
                _ => false
            };
        }



        #endregion

        #region Item index cahce
        private readonly Dictionary<ItemTypeSO, List<int>> itemIndexCache = new();

        private void CacheAdd(ItemTypeSO itemInfo, int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (!itemIndexCache.TryGetValue(itemInfo, out var list))
            {
                list = new List<int>();
                itemIndexCache[itemInfo] = list;
            }

            if (!list.Contains(index))
                list.Add(index);
        }

        private void CacheAdd(IGameItem item, int index)
        {
            if (item?.GetItemInfo is not ItemTypeSO data) return;
            CacheAdd(data, index);
        }

        private void CacheRemove(IGameItem item, int index)
        {
            if (item?.GetItemInfo is not ItemTypeSO data) return;
            if (!itemIndexCache.TryGetValue(data, out var list)) return;

            list.Remove(index);
            if (list.Count == 0)
                itemIndexCache.Remove(data);
        }

        // 슬롯 하나를 통째로 비울 때 사용하면 편한 래퍼
        private void CacheClearSlot(IGameItemSlot slot)
        {
            if (slot is { HasItem: true, GetItem: { } item })
            {
                CacheRemove(item, slot.Index);
            }
        }

        // “해당 ItemTypeSO를 가진 슬롯이 있는지” 캐시에서 먼저 찾기
        private bool TryGetCachedIndex(ItemTypeSO data, int minIndex, out int index)
        {
            index = -1;
            if (data == null) return false;
            if (!itemIndexCache.TryGetValue(data, out var list)) return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                int idx = list[i];

                // 시작 인덱스보다 앞이면 스킵
                if (idx < minIndex) continue;

                // 슬롯 인덱스 자체가 유효하지 않으면 캐시에서 제거
                if (!IsValidSlotIdx(idx))
                {
                    list.RemoveAt(i);
                    continue;
                }

                // 실제 슬롯 내용과 데이터 일치 여부 lazy 검증
                if (slots[idx] is { HasItem: true, GetItemInfo: ItemTypeSO slotData } && slotData == data)
                {
                    index = idx;
                    return true;
                }

                // 내용이 바뀌었으면 캐시에서 제거
                list.RemoveAt(i);
            }

            if (list.Count == 0)
                itemIndexCache.Remove(data);

            return false;
        }

        private void RebuildItemIndexCache()
        {
            itemIndexCache.Clear();

            int end = GetEndIdx;
            if (end < 0) return;

            for (int i = 0; i <= end; i++)
            {
                if (slots[i] is { IsAccessible: true, HasItem: true, GetItem: { } item })
                {
                    CacheAdd(item, i);
                }
            }
        }


        #endregion

        private void Clear()
        {
            slots.Clear();
            FillInventoryWithEmptySlots();

            countableDict.Clear();
            itemIndexCache.Clear();
        }
    }
}


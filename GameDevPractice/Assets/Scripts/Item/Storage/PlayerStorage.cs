using System;
using System.Collections.Generic;
using RPG.Saving;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using TH.Resource;
using TH.SaveLoad;
using UnityEngine.SceneManagement;
using TH.Item.Storage;

namespace TH.Item
{
    public sealed class PlayerStorage : IPlayerStorage, ISavableWithId
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

        private CharacterTypeHolder player;
        
        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        public PlayerStorage()
        {
            SetCapacity(InitialCapacity);
            FillInventoryWithEmptySlots();
            
            ResourceManager.Instance.WaitForPreLoad(() =>
            {
                InventoryTestData testData =
                    ResourceManager.Instance.Load<GameObject>("InventoryTestData.prefab").GetComponent<InventoryTestData>();

                if (testData == null)
                {
                    Logg.Log("TestData is null");
                    return;
                }
             
                foreach (var item in testData.items)
                { 
                    Logg.Log($"Trying to add ({item.GetItemInfo.nameString}, {item.GetAmount})", Logg.LoggingMode.Completed);
                    if (!TryStore(EnsureItemInstanceByType(item.GetItemInfo, item.GetAmount)))
                    {
                        Logg.Log($"[PlayerInventory] failed to add test data item '{item}'");
                    }
                }
                
                ServiceLocator.Get<ISaveSystem>().Register(this);
            });

            SceneManager.sceneLoaded += (_, _) =>
            {
                player = GameObject.FindGameObjectWithTag("Player").GetComponent<CharacterTypeHolder>();
            };
            
            OnStorageChanged?.Invoke();
        }
        
        #region Store (Take in)

        public bool TryStore(IGameItem item)
        {
            if (EnsureItemInstanceByType(item) is { } modified &&
                FindEmptySlotIndex(0) is { } index and >= 0)
            {
                if (item is CountableItem cItem)
                    return TryStore(cItem, cItem.GetAmount, out var excess); //todo: 초과분 발생시 처리 추가, 현재는 초과분이 소실
                return TryStore(modified, index);
            }

            return false;
        }

        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            if (EnsureItemInstanceByType(item) is { } modified &&
                FindEmptySlotIndex(0) is { } index and >= 0)
            {
                bool result = TryStore(modified, index);
                storedSlot = result ? slots[index] : null;
                return result;
            }

            storedSlot = null;
            return false;
        }

        public bool TryStore(IGameItem item, int index)
        {
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: false } slot) return false;
            
            var result = slot.TryStore(item);
            if (result) NotifySlotChanged(index);
            return result;
        }

        #endregion

        #region IUsableItemStorage
        
        public bool TryStoreAndUse(IGameItem item, object user = null)
        {
            if (!TryStore(item, out var storedSlot)) return false;
            
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

        #region ICountableItemStorage

        public bool TryStore(ICountableItem countableItem, int amount, out int excess)
        {
            if (countableItem == null || amount <= 0)
            {
                excess = 0;
                return false;
            }
            excess = amount;

            // 1) 동일 스택에 병합 (가득 찰 때까지)
            int start = 0;
            while (excess > 0)
            {
                if (!FindIdenticalCountable(countableItem, start, out var foundSlot)) break; // 인벤토리 내 동일 아이템이 없다면 루프 탈출
                TryAdd(foundSlot, excess, out excess); // 개수 전달에 성공한 경우 남은 양(excess)에 반영
                start = foundSlot.Index + 1; // 다음 루프에서는 찾은 슬롯 다음 슬롯부터 탐색 시작
            }
            
            if (excess <= 0) return true; // 남은 양이 없으면 return true;
            
            // 2) 남은 양을 빈 슬롯에 신규 스택으로 채우기
            int maxStack = countableItem.GetItemInfo.maxAmount > 0
                ? countableItem.GetItemInfo.maxAmount
                : int.MaxValue;

            while (excess > 0)
            {
                int idx = FindEmptySlotIndex(0);
                if (idx < 0) break;
                
                int put = Mathf.Min(excess, maxStack);
                // 아이템의 복사 객체를 만들고 {put}만큼 개수 설정 + 기존 아이템의 개수 조정 ({기존 개수} - {put})
                var clone = countableItem.Clone<ICountableItem>(put); 
                
                if (clone == null) break;
                if (!TryStore((IGameItem)clone, idx)) break;
                
                NotifySlotChanged(idx);
                excess -= put;
            }
            
            return excess <= 0;
        }

        private bool TryAdd(IGameItemSlot foundSlot, int amount, out int overflow)
        {
            if (foundSlot.GetItem is not ICountableItem cItem)
            {
                overflow = amount;
                return false;
            }
            
            overflow = cItem.AddAmount(amount);
            int used = amount - overflow;
            if (used <= 0) return false;
            
            NotifySlotChanged(foundSlot.Index);
            return true;
        }

        public bool TryAdd(ICountableItem countableItem, int amount, int index, out int excess)
        {
            if (countableItem == null || countableItem.IsEmpty || amount <= 0)
            {
                excess = amount;
                return false;
            }
            
            excess = amount;
            if (GetSlot(index) is
                    { IsAccessible: true, HasItem: true, GetItem: { Type: Enums.ItemType.Countable } slotItem } slot
                && countableItem.IsEqual(slotItem, ItemComparerExtension.ItemCompareMode.CompareData))
                return TryAdd(slot, amount, out excess);
            
            return false;
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
                || !TryStore(clone, emptySlot.Index))
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
            if (slots[index] is not { IsAccessible: true, HasItem: true } slot) return false;
            
            var result = slot.Clear();
            if (result) NotifySlotChanged(index);
            
            return result;
        }

        public bool TryRemoveItem(int index, out IGameItem item)
        {
            Logg.Log($"[PlayerStorage] TryRemove({index}) invoked", Logg.LoggingMode.InProgress);
            item = default;
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true } slot) return false;
            if (!slot.Clear(out var stored)) return false;

            item = stored;
            NotifySlotChanged(index);
            return true;
        }

        #endregion
        
        #region Transfer/Swap
        
        // 스토리지 내부 슬롯 간 아이템 이동
        public bool TryTransferItem(IGameItemSlot from, IGameItemSlot to)
        {
            if (from is not { IsAccessible: true, HasItem: true,
                    Index: { } fromIndex, GetItem: { } fromItem } || !IsValidSlotIdx(fromIndex) || 
                to is not { IsAccessible: true, 
                    Index: { } toIndex } || !IsValidSlotIdx(toIndex)) 
                return false; // 출발 슬롯과 도착 슬롯 중에 유효하지 않은 슬롯이 존재할 경우 실패

            if (to is { HasItem: true, GetItem: { } toItem }) // 도착 슬롯에 기존 아이템이 있는 경우 -> 아이템 자리 교체 (Swap)
                return SwapItem(from, fromItem, to, toItem);
            
            return TransferItem(from, fromItem, to); // ** 없는 경우 -> 단순 아이템 이동
        }

        public bool TryTransferItem(int fromIdx, int toIdx)
        {
            if (!IsValidSlotIdx(fromIdx) || !IsValidSlotIdx(toIdx)) return false;
            return TryTransferItem(slots[fromIdx], slots[toIdx]);
        }

        private bool TransferItem(IGameItemSlot fromSlot, IGameItem fromItem, IGameItemSlot toSlot)
        {
            if (toSlot.TryStore(fromItem) && fromSlot.Clear()) {
                NotifySlotChanged(fromSlot.Index);
                NotifySlotChanged(toSlot.Index);
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
                NotifySlotChanged(fromSlot.Index);
                NotifySlotChanged(toSlot.Index);
                return true; // 아이템 슬롯 간 아이템 교환 시도
            }

            // 교환 실패 시 원복
            fromSlot.TryStore(fromItem, byForce: true);
            toSlot.TryStore(toItem, byForce: true);
            return false;
        }
        
        #endregion
        
        #region Compare

        private static bool IsSameItem(IGameItem a, IGameItem b) // 정확히 동일한 아이템인지 검사 (ItemTypeSO 기준)
        {
            return (a?.GetItemInfo == b?.GetItemInfo);
        }

        private bool IsSameType(IGameItem a, IGameItem b) // 동일한 타입인지 검사 (Enums.ItemType 기준)
        {
            return (a?.GetItemInfo.itemType == b?.GetItemInfo.itemType);
        }

        private bool IsSameEquipmentType(IGameItem a, IGameItem b) // 장비 대분류가 동일한지 검사 (Enums.EquipmentType 기준)
        {
            if (a?.GetItemInfo is EquipmentTypeSO aData && b?.GetItemInfo is EquipmentTypeSO bData)
            {
                return aData.equipmentType == bData.equipmentType;
            }
            
            return false;
        }

        private bool IsSameEquipSlotType(IGameItem a, IGameItem b) // 장착 슬롯 종류가 동일한지 검사 (Enums.EquippedSlotType 기준)
        {
            if (a?.GetItemInfo is EquipmentTypeSO aData && b?.GetItemInfo is EquipmentTypeSO bData)
            {
                return aData.slotType == bData.slotType;
            }
            
            return false;
        }

        #endregion
        
        #region Slot

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

        private int FindEmptySlotIndex(int start = 0) // 빈 인벤토리 슬롯 탐색
        {
            int end = GetEndIdx;
            for (int i = start; i <= end; i++)
            {
                switch (slots[i])
                {
                    case null:
                        slots[i] = MakeEmptySlot(index: i); // 해당 인덱스에 최초 접근시, ItemSlot 인스턴스 생성
                        return i;
                    case { IsAccessible: true, HasItem: false }:
                        return i;
                }
            }
            
            return -1; // 빈칸이 없으면 -1 반환
        }

        private int FindIdenticalCountable(ICountableItem cItem, int start = 0)
        {
            if (!IsValidSlotIdx(start)) return -1;
            int end = GetEndIdx;
            
            for (int i = start; i < end; i++)
            {
                if (!IsIdenticalCountableItem(cItem, i, out var v)) continue;
                return i; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환
            }

            return -1;
        }

        private bool FindIdenticalCountable(ICountableItem cItem, int start, out IGameItemSlot slot)
        {
            slot = null;
            if (!IsValidSlotIdx(start)) return false;
            int end = GetEndIdx;
            
            for (int i = start; i <= end; i++)
            {
                if (!IsIdenticalCountableItem(cItem, i, out var found)) continue;
                slot = found; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환
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
                if (GetSlot(i) is not { } slot) continue;
                slot.SetVisibility(true);
                slot.SetAccessibility(true);
            }
        }

        #endregion

        #region ISavable(save/load)

        private const string inventoryIdentifier = "playerInventory";
        public string UniqueIdentifier => inventoryIdentifier;
        
        public object CaptureState()
        {
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
            if (state is not List<IGameItem> items) return false;

            foreach (var item in items)
            {
                TryStore(itemBuilder.GetItemFromData(item.GetItemInfo, item.GetAmount));
            }

            return true;
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
                Logg.Log($"[{nameof(PlayerStorage)}] item slot list has something before initialization", Logg.LoggingMode.InProgress);
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

        #region Filter

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
    }
}


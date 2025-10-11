using System;
using System.Collections.Generic;
using RPG.Item;
using RPG.Saving;
using TH.Utils;
using UnityEngine;
using TH.Resource;

namespace TH.Item
{
    public sealed class PlayerInventory : IPlayerInventory, ISavable
    {
        public event Action<int> OnStoredItemChanged;
        public event Action OnStorageChanged;
        public event Action<int> OnCapacityChanged;
        
        public event Action<InventorySystem.InventoryFilterType> OnInventoryFilterChanged;
        
        public InventorySystem.InventoryFilterType CurrFilter { get; }
        
        public IReadOnlyCollection<IGameItemSlot> ItemSlots => slots;
        private List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: maxCapacity);
        private readonly Dictionary<ItemTypeSO, int> countableDict = new(); // CountableItem의 종류별 개수 (trim, sort 최적화 목적)

        public int Capacity => capacity;
        public int MaxCapacity => maxCapacity;
        private int capacity;
        private const int InitialCapacity = 80;
        private const int maxCapacity = 256;

        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        
        public PlayerInventory()
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
                    Logg.Log($"Trying to add {item.GetItemInfo.nameString}", Logg.LoggingMode.InProgress);
                    if (!TryStore(EnsureItemInstanceByType(item.GetItemInfo, item.GetAmount)))
                    {
                        Logg.Log($"[PlayerInventory] failed to add test data item '{item}'");
                    }
                }
            });
        }
        
        #region Store (Take in)

        public bool TryStore(IGameItem item)
        {
            item = EnsureItemInstanceByType(item);
            int idx = FindEmptySlotIndex(0);
            if (idx < 0) return false;

            return TryStore(item, idx);
        }

        public bool TryStore(IGameItem item, int index)
        {
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: false } slot) return false;
            
            var result = slot.TryStore(item);
            if (result) OnStoredItemChanged?.Invoke(index);
            return result;
        }

        public bool TryStore(IGameItem item, int amount, out int excess)
        {
            int index = -1;
            int initialAmount = item.GetAmount * amount;
            int remain = initialAmount;
            if (item is ICountableItem cItem)
            {
                bool findEquivalentCountable = true;

                while (remain > 0)
                {
                    if (findEquivalentCountable)
                    {
                        index = FindIdenticalCountable(cItem, index + 1);
                        if (index == -1) findEquivalentCountable = false;
                        else
                        {
                            remain = (slots[index].GetItem as ICountableItem)?.AddAmount(remain) ?? 0;
                            OnStoredItemChanged?.Invoke(index);
                        }
                    }
                    else
                    {
                        index = FindEmptySlotIndex(index + 1);
                        if (index == -1)
                        {
                            UpdateCountableDict(remain);
                            excess = remain;
                            return true;
                        }

                        int maxAmount = cItem.GetItemInfo.maxAmount;
                        int storingAmount = Mathf.Min(remain, maxAmount);
                        cItem.SetAmount(storingAmount);
                        
                        if (!slots[index].TryStore(cItem)) // 저장에 실패했다면 루프 중단
                            break;
                        
                        remain -= storingAmount; // 저장에 성공했다면 남은 개수 차감
                    }
                }
                
                UpdateCountableDict(remain);
                excess = remain;
                return true;
            }

            while (remain > 0)
            {
                index = FindEmptySlotIndex(index + 1);
                if (index == -1) break; // 빈칸을 찾지 못한 경우 -> 루프 탈출
                if (!slots[index].TryStore(item.Clone<IGameItem>())) break; // 빈칸에 아이템 저장 실패 -> 루프 탈출
                // 빈칸에 아이템 저장 성공 시 
                remain--;
                //todo: UseImmediately 옵션
                OnStoredItemChanged?.Invoke(index);
            }

            excess = remain;
            return true;

            void UpdateCountableDict(int num) // num: TryStore() 중단 시점 남은 개수
            {
                if (item.GetItemInfo is not { } itemInfo) return;
                int stored = initialAmount - num;
                if (countableDict.TryGetValue(itemInfo, out var v))
                {
                    stored += v;
                }

                countableDict[itemInfo] = stored;
                Logg.Log($"[{nameof(InventorySystem)}.{nameof(UpdateCountableDict)}()] ({itemInfo}, {stored})", Logg.LoggingMode.Completed);
            }
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

        public bool TryGetItem(object key, out IGameItem item)
        {
            Logg.LogError($"[{nameof(PlayerInventory)}] object type key not supported");
            item = null;
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

        public bool TryGetItemSlot(object key, out IGameItemSlot itemSlot)
        {
            Logg.LogError($"[{nameof(PlayerInventory)}] object type key not supported");
            itemSlot = null;
            return false;
        }

        #endregion

        #region Remove (Take out)

        public bool TryRemoveItem(int index)
        {
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true } slot) return false;
            
            return slot.Clear();
        }

        public bool TryRemoveItem(object key)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveItem(int index, out IGameItem item)
        {
            item = default;
            if (!IsValidSlotIdx(index)) return false;
            if (slots[index] is not { IsAccessible: true, HasItem: true } slot) return false;
            if (!slot.Clear(out var stored)) return false;

            item = stored;
            return true;
        }

        public bool TryRemoveItem(object key, out IGameItem item)
        {
            throw new NotImplementedException();
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
                OnStoredItemChanged?.Invoke(fromSlot.Index);
                OnStoredItemChanged?.Invoke(toSlot.Index);
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
                OnStoredItemChanged?.Invoke(fromSlot.Index);
                OnStoredItemChanged?.Invoke(toSlot.Index);
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

        private IGameItemSlot GetSlot(int index)
        {
            if (!IsValidSlotIdx(index)) return null;
            return slots[index];
        }

        private int FindEmptySlotIndex(int start = 0) // 빈 인벤토리 슬롯 탐색
        {
            int end = GetEndIdx;
            for (int i = start; i < end; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = MakeEmptySlot(index: i); // 해당 인덱스에 최초 접근시, ItemSlot 인스턴스 생성
                    return i;
                }
                
                if (slots[i] is { IsAccessible: true, HasItem: false } )
                    return i;
            }
            
            return -1; // 빈칸이 없으면 -1 반환
        }

        private int FindIdenticalCountable(ICountableItem cItem, int start = 0)
        {
            if (!IsValidSlotIdx(start)) return -1;
            int end = GetEndIdx;
            
            for (int i = start; i < end; i++)
            {
                var itemSlot = slots[i];
                if (itemSlot is not { HasItem: true, GetItemInfo: {itemType: Enums.ItemType.Countable} }) continue;
                // if (itemSlot.GetItemInfo.itemType != Enums.ItemType.Countable) continue;
                if (!IsSameItem(itemSlot.GetItem, cItem)) continue;

                return i; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환
            }

            return -1;
        }
        
        public bool SetCapacity(int capa, bool byForce = false)
        {
            if (capa > maxCapacity || capa == capacity) return false; // 현재 capacity와 동일하거나 최대 capacity를 초과하면 false
            
            capacity = capa;
            OnCapacityChanged?.Invoke(capa);
            return true;
        }

        #endregion

        #region ISavable(save/load)

        public object CaptureState()
        {
            throw new NotImplementedException();
        }

        public bool RestoreState(object state)
        {
            throw new NotImplementedException();
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
                Logg.Log($"[{nameof(PlayerInventory)}] item slot list has something before initialization", Logg.LoggingMode.InProgress);
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

        private IGameItem EnsureItemInstanceByType(IGameItem item)
        {
            if (item is not { GetItemInfo: { itemType: { } type } })
            {
                Logg.LogError($"[PlayerInventory] failed to Make GameItem Instance");
                return null;
            }
            switch (type)
            {
                case Enums.ItemType.Countable:
                    if (item is CountableItem) return item;
                    return new CountableItem(item.GetItemInfo, item.GetAmount);
                case Enums.ItemType.Equipment:
                    return new EquipmentItem(item.GetItemInfo);
                default:
                    return item;
            }
        }
        
        private IGameItem EnsureItemInstanceByType(ItemTypeSO data, int amount = 1)
        {
            if (data is not { itemType: { } type })
            {
                Logg.LogError($"[PlayerInventory] failed to Make GameItem Instance");
                return null;
            }
            switch (type)
            {
                case Enums.ItemType.Countable:
                    return new CountableItem(data, amount);
                case Enums.ItemType.Equipment:
                    return new EquipmentItem(data);
                default:
                    return new GameItem(data);
            }
        }

        #endregion

        
    }
}


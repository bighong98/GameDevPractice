using System;
using System.Collections.Generic;
using UnityEngine;
using RPG.Combat;
using RPG.Saving;
using RPG.UI;
using TH.Core.Service;
using TH.Item;

namespace RPG.Item
{
    public class InventorySystem: MonoBehaviour, ISavable, IInventorySystem
    {
        // Capacity (인벤토리 슬롯 개수)
        public int Capacity { get; private set; } // SetCapacity()로만 변경할 것
        public int MaxCapacity =>  maxCapacity;
        private const int maxCapacity = 256; // 인벤토리 최대 칸수
        [SerializeField, Range(8, maxCapacity)] private int initialCapacity = 80; //실제론 inspector 값이 들어가니 주의 //todo: Constants에서 선언하고 사용할지 고민
        
        // item data container (itemSlot)
        private ItemSlot[] inventoryItems; // 인벤토리에 보관된 아이템 목록
        private EquipmentSlot[] equippedItems; // 장착 중인 장비(무기, 방어구) 목록 // todo: 장착된 장비 능력치 반영
        private readonly Dictionary<ItemTypeSO, int> countableDict = new(); // CountableItem의 종류별 개수 (trim, sort 최적화 목적)
        
        // itemSlot Delegate
        public event Action<int> OnCapacityChanged; // 인벤토리의 칸 수가 변경된 경우
        public event Action<int> OnInventorySlotChanged; // 1개의 인벤토리 슬롯 초기화가 필요한 경우 (인덱스 접근)
        public event Action<int> OnEquippedSlotChanged; // 1개의 장비 슬롯 초기화가 필요한 경우 (인덱스 접근)
        public event Action OnInventoryChanged; // 인벤토리 전체 초기화가 필요한 경우
        // public event Action OnEquippedChanged; // 장착 슬롯 전체 초기화가 필요한 경우
        public event Action<InventorySystem.InventoryFilterType> OnInventoryFilterChanged;
        
        
        private InventoryFilterType currFilter = InventoryFilterType.All;
        public InventoryFilterType CurrentFilter => currFilter; // 외부 접근용 프로퍼티
        
        private void Awake()
        {
            ServiceLocator.Register<IInventorySystem>(this);
            
            Capacity = initialCapacity;
            inventoryItems = new ItemSlot[maxCapacity];
            equippedItems = new EquipmentSlot[(int)Enums.EquippedItemSlotType.Max];
            
            //todo: 장비 착용 관련 로직 처리방식 결정 및 구현
            
            // 빈 인벤토리 슬롯 초기화
            for (int i = 0; i < Capacity; i++)
            {
                inventoryItems[i] = MakeEmptyItemSlot(index: i);
            }
            
            // 빈 장비 슬롯 초기화
            for (int i = 0; i < (int)Enums.EquippedItemSlotType.Max; i++)
            {
                equippedItems[i] = new EquipmentSlot(
                    null, i,
                    equippedValidItemTypes, true,
                    validEquipSlotType: (Enums.EquippedItemSlotType) i
                    );
                equippedItems[i].OnEquipmentChanged += this.OnEquipmentChanged;
            }
            
            ResourceManager.Instance.WaitForPreLoad((_) =>
            {
             InventoryTestData testData =
                 ResourceManager.Instance.Load<GameObject>("InventoryTestData.prefab").GetComponent<InventoryTestData>();

             if (testData == null)
             {
                 Util.Log("TestData is null");
                 return;
             }
             
             foreach (var item in testData.items)
             {
                Util.Log($"Trying to add {item.GetItemInfo.nameString}", Util.LoggingMode.Completed);
                AddItem(item, checkInstanceType: true);
             }
            });
        }

        #region Capacity

        public void SetCapacity(int capa)
        {
            if (Capacity == capa || capa > maxCapacity) return;
            if (Capacity > capa) // case: 인벤토리 칸 감소
            {
                for (int i = capa; i < Capacity; i++)
                {
                    inventoryItems[i].SetAccessibility(false); // 감소된 칸 만큼 슬롯 비활성화 (뒤에서부터)
                }
            }
            else // case: 인벤토리 칸 증가
            {
                for (int i = Capacity; i < capa; i++)
                {
                    inventoryItems[i] = MakeEmptyItemSlot(index: i); 
                }
            }

            Capacity = capa;
            OnCapacityChanged?.Invoke(Capacity);
        }

        #endregion

        #region Read Slot

        public IEnumerable<ItemSlot> ReadOnlyInventorySlots => inventoryItems;
        public IEnumerable<EquipmentSlot> ReadOnlyEquippedSlots => equippedItems;

        private int ValidInventoryEndIndex => Mathf.Min(Capacity, inventoryItems.Length);
        
        private bool IsValidInventorySlot(int index)
        {
            return (index >= 0 && index < Mathf.Min(Capacity, inventoryItems.Length)); // todo: Length가 GC 발생시키는지 확인
        }

        private bool IsValidEquippedSlot(int index)
        {
            return index >= 0 && index < equippedItems.Length; // todo: Length가 GC 발생시키는지 확인
        }

        private bool IsEmptySlot(int index) // 인벤토리 슬롯이 비어있는지 확인
        {
            if (!IsValidInventorySlot(index)) return false;

            return inventoryItems[index] is null or { IsAccessible: true, HasItem: false }; // 해당 인덱스에 생성된 ItemSlot 인스턴스가 없거나, 아이템 개수가 0으로 설정된 경우(CountableItem 한정)
        }
        
        public ItemSlot GetInventorySlot(int index)
        {
            // Util.Log($"Trying to GetInventorySlot: {index}");
            if (!IsValidInventorySlot(index)) return null;
            if (inventoryItems[index] is { IsAccessible: true } slot)
            {
                return slot;
            }
            
            Util.Log($"[InventorySystem.GetInventorySlot()] return null");
            return null;
        }
        
        public ItemSlot GetEquippedSlot(int index)
        {
            if (!IsValidEquippedSlot(index)) return null;
            if (equippedItems[index] is { IsAccessible: true } slot)
            {
                return slot;
            }
            
            Util.Log($"[InventorySystem.GetEquippedSlot()] return null");
            return null;
        }
        
        public int GetItemAmount(int index) // 인벤토리 슬롯에 저장된 아이템 개수 확인 (CountableItem 타입에 사용 목적)
        {
            if (!IsValidInventorySlot(index)) return 0;
            if (inventoryItems[index] is { IsAccessible:true, IsValid: true, HasItem:true } itemSlot )
            {
                return itemSlot.GetAmount;
            }
            
            return 0;
        }

        public bool CanStore(ItemSlotBaseUI fromSlotUI, ItemSlotBaseUI toSlotUI)
        {
            return CanStore(FindUITargetSlot(fromSlotUI), FindUITargetSlot(toSlotUI));
        }
        
        public bool CanStore(ItemSlot fromSlot, ItemSlot toSlot)
        {
            if (fromSlot is null or { HasItem: false } ) return false;
            if (toSlot == null) return false;

            return toSlot.CanStore(fromSlot.GetItemInfo);
        }
        
        #endregion

        #region Find Slot
        private int FindEmptySlotIndex(int start = 0) // 빈 인벤토리 슬롯 탐색
        {
            int end = ValidInventoryEndIndex;
            for (int i = start; i < end; i++)
            {
                if (inventoryItems[i] == null)
                {
                    inventoryItems[i] = MakeEmptyItemSlot(index: i); // 해당 인덱스에 최초 접근시, ItemSlot 인스턴스 생성
                    return i;
                }
                
                if (inventoryItems[i] is { IsAccessible: true, HasItem: false } )
                    return i;
            }
            
            return -1; // 빈칸이 없으면 -1 반환
        }
        
        private int FindCountableItemSlotIndex(CountableItem cItem, int start = 0) // 동일한 CountableItem을 보관중인 슬롯 탐색
        {
            if (!IsValidInventorySlot(start)) return -1;
            int end = ValidInventoryEndIndex;
            for (int i = start; i < end; i++)
            {
                var itemSlot = inventoryItems[i];
                if (itemSlot is not { HasItem: true }) continue;
                if (itemSlot.GetItemInfo.itemType != Enums.ItemType.Countable) continue;
                if (!IsSameItem(itemSlot.GetItem, cItem)) continue;

                return i; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환
            }

            return -1; // 인벤토리에 동일 아이템이 없는 경우 -1 반환 (=실패)
        }
        
        public ItemSlot FindUITargetSlot(ItemSlotBaseUI slotUI)
        {
            if (slotUI is EquipmentSlotUI)
            {
                Util.Log($"[FindUITargetSlot] UI_EquipmentSlot index:{slotUI.Index}", Util.LoggingMode.Completed);
                if (!IsValidEquippedSlot(slotUI.Index)) return null;
                
                return equippedItems[slotUI.Index];
            }
            else
            {
                if (!IsValidInventorySlot(slotUI.Index)) return null;
                if (inventoryItems[slotUI.Index] == null)
                {
                    inventoryItems[slotUI.Index] = MakeEmptyItemSlot(slotUI.Index, ItemSlotValidationStandard.All);
                }
                
                return inventoryItems[slotUI.Index];
            }
        }

        private EquipmentSlot FindSuitableEquipSlot(Item item)
        {
            if (item?.GetItemInfo is EquipmentTypeSO equipmentSO)
            {
                // todo: 동일 타입의 장비슬롯이 복수 존재할 수 있을 경우, 로직 변경 필요
                int idx = (int)equipmentSO.slotType;
                if (IsValidEquippedSlot(idx))
                {
                    return equippedItems[idx];
                }
            }

            return null;
        }
        
        #endregion

        #region Trim/Sort/Filter Inventory

        public void CompressInven(bool sort)
        {
            int write = TrimInven(sort);
            if (sort) SortInven(write);
            
            OnInventoryChanged?.Invoke();
        }
        
        private int TrimInven(bool combineStackables)
        {
            int last = 0;
            int cap = Capacity;
            for (int curr = 0; curr < cap; curr++)
            {
                try
                {
                    if (inventoryItems[curr] is not {} itemSlot) continue;
                    if (itemSlot is { HasItem: false } ) continue; // 빈 슬롯인 경우 패스
                    if (combineStackables && itemSlot.GetItemInfo.itemType == Enums.ItemType.Countable) continue;
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(InventorySystem)}.{nameof(TrimInven)}TrimInven: index: {curr}, {e.Message}");
                    return 0;
                }
                
                OverwriteSlot(inventoryItems[curr], inventoryItems[last]);
                last++;
            }

            if (combineStackables)
            {
                last = CombineStackables(last);
            }
            
            for (int i = last; i < cap; i++)
            {
                inventoryItems[i].Clear();
            }

            return last;
        }

        
        private int CombineStackables(int last)
        {
            int curr = last;

            foreach ((var itemData, int amount) in countableDict)
            {
                Util.Log($"[{nameof(InventorySystem)}.{nameof(CombineStackables)}()] ({itemData.nameString}, {amount})", Util.LoggingMode.Completed);
                int remain = amount;
                while (remain > 0)
                {
                    int storingAmount = Mathf.Min(remain, itemData.maxAmount);
                    if (!inventoryItems[curr].Store(new CountableItem(itemData, storingAmount), true)) continue;
                    
                    remain -= storingAmount;
                    curr++;
                }
            }

            return curr; // Trim -> Combine Countable Item Stack -> 아이템이 있는 인벤토리 칸 수 반환
        }

        
        private void SortInven(int last)
        {
            Array.Sort(inventoryItems, 0, last, itemSortingComparer);
            for (int i = 0; i < last; i++)
            {
                inventoryItems[i].Index = i;
            }
        }

        public enum InventoryFilterType
        {
            All,
            Equipment,
            Consumable,
            Resource,
        }

        private void FilterInven(InventoryFilterType filter)
        {
            if (currFilter == filter) return;
            currFilter = filter;
            
            for (int i = 0; i < Capacity; i++)
            {
                var slot = inventoryItems[i];
                slot.SetVisibility(IsVisibleByFilter(slot, filter));
            }

            OnInventoryFilterChanged?.Invoke(filter);
        }

        public static bool IsVisibleByFilter(ItemSlot slot, InventoryFilterType filter)
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
        
        #region Add/Remove/Transfer/Use(Consume+Equip) Item
        
        // 인벤토리에 아이템 추가
        // item: 인벤토리에 추가하려는 아이템(데이터 SO + 개수(CountableItem만 적용))
        // amount: 추가하려는 아이템의 개수
        // checkInstanceType: 인스턴스 재생성 확인 여부 (한 번에 여러개의 아이템을 추가하는 경우는 거의 true로 사용, 외부파일/테이블 포함)
        // useImmediately: 인벤토리에 추가 후 즉시 사용(소비 혹은 장착) 여부 (CountableItem은 불가능)
        public int AddItem(Item item, int amount = 1, bool checkInstanceType = false, bool useImmediately = false) // 인벤토리 슬롯에 아이템 추가, CountableItem
        {
            if (checkInstanceType)
            {
                item = ModifyItemInstanceByType(item); // 아이템 타입에 적합한 RPG.Item.Item의 하위 클래스 인스턴스로 재생성
            }
            if ((!item?.IsValid) ?? true) return 0;
            
            int index = -1; // 현재 탐색 중인 기준 인덱스
            int initialAmount = amount; // 루프 전 최초 아이템 수량 (CountableItem 종류별 총 개수 파악 목적, countableDict)
            
            if (item is CountableItem countItem) // case: 아이템이 Countable 타입인 경우
            {
                bool findEquivalentCountable = true;
                amount *= countItem.GetAmount; // CountableItem의 경우 Item 객체가 1 이상의 개수를 가질 수 있으므로 곱하여 실제 개수 반영
                initialAmount = amount;
                
                while (amount > 0)
                {
                    if (findEquivalentCountable)
                    {
                        index = FindCountableItemSlotIndex(countItem, index + 1); // 동일한 아이템 조회
                        if (index == -1) // -1은 조회 실패를 의미
                        {
                            findEquivalentCountable = false;
                        }
                        else // 인벤토리 내에서 동일한 CountableItem을 찾은 경우
                        {
                            // 개수 추가 및 슬롯 최대개수 초과량을 반환
                            amount = (inventoryItems[index].GetItem as CountableItem)?.AddAmount(amount) ?? 0;
                            OnInventorySlotChanged?.Invoke(index);
                        }
                    }
                    else // 한도수량에 도달하지 않은 동일 아이템이 존재하지 않는 경우, 빈 슬롯 탐색
                    {
                        index = FindEmptySlotIndex(index + 1);
                        if (index == -1)
                        {
                            UpdateCountableDict(amount);
                            return amount; // 빈 슬롯이 없는 경우, 남은 개수 반환
                        }

                        int maxAmount = countItem.GetItemInfo.maxAmount;
                        int storingAmount = Mathf.Min(amount, maxAmount);
                        
                        countItem.SetAmount(storingAmount);
                        if (inventoryItems[index].Store(countItem)) // 슬롯에 아이템 저장 성공시, 저장한 개수만큼 차감
                        {
                            amount -= storingAmount;
                            continue;
                        }
                        // 실패시 즉시 남은 개수 반환
                        UpdateCountableDict(amount);
                        return amount;
                    }
                }

                UpdateCountableDict(amount);
                return amount;
            }

            // 수량이 없는 아이템
            while (amount > 0)
            {
                index = FindEmptySlotIndex(index + 1);
                if (index == -1) break; // 빈칸을 찾지 못한 경우, 루프 탈출

                if (inventoryItems[index].Store(item.Clone<Item>())) // 빈칸에 아이템 저장 성공 시 
                {
                    amount--; // 남은 개수 -1
                    if (useImmediately)
                    {
                        UseItem(inventoryItems[index]);
                    }
                    NotifySlotUpdated(inventoryItems[index]); // 해당 슬롯 데이터 변동 알림
                    continue;
                }
                
                return amount; // 아이템 저장 실패 시, 남은 개수 즉시 반환
            }
            
            return amount;

            void UpdateCountableDict(int remain) // 인벤토리에 추가된 CountableItem 개수 딕셔너리에 반영 
            {
                if (item.GetItemInfo is not { } itemInfo) return;
                int stored = initialAmount - remain;
                if (countableDict.TryGetValue(itemInfo, out var v))
                {
                    stored += v;
                }

                countableDict[itemInfo] = stored;
                Util.Log($"[{nameof(InventorySystem)}.{nameof(UpdateCountableDict)}()] ({itemInfo}, {stored})", Util.LoggingMode.Completed);
            }
        }
        
        private void RemoveItem(int index, bool byForce = false) // 해당 인벤토리 인덱스 슬롯의 아이템 제거 (슬롯 비우기)
        {
            if (!IsValidInventorySlot(index)) return;
            if (!byForce) // byForce: true -> IsAccessible, Special 타입 여부 무시하고 제거
            {
                if (!inventoryItems[index].IsAccessible) return;
                if (inventoryItems[index].GetItemInfo.itemType == Enums.ItemType.Special) return;
            }

            if (inventoryItems[index].Clear()) // 슬롯 아이템 삭제 성공 시
            {
                OnInventorySlotChanged?.Invoke(index); // 슬롯 상태 변경 알림
            }
        }

        public void RemoveItem(ItemSlotBaseUI slotUI) // 슬롯UI에 해당하는 아이템 슬롯 비우기
        {
            if (FindUITargetSlot(slotUI) is not { } slot ) return; // 슬롯UI에 해당하는 아이템 슬롯 인스턴스를 찾지 못한 경우 실행 취소
            if (slot is EquipmentSlot) return; // 장비 슬롯인 경우 취소
            
            RemoveItem(slot.Index, false);
        }

        public bool TransferItem(ItemSlot fromSlot, ItemSlot toSlot) // fromSlot에 있는 아이템을 toSlot으로 옮기기
        {
            if (fromSlot is not { HasItem: true } || toSlot == null) return false; // fromSlot이 비어있거나, toSlot이 null이면 실패
            
            try
            {
                if (fromSlot.GetItem is CountableItem fromCountItem &&
                    toSlot.GetItem is CountableItem toCountItem &&
                    IsSameItem(fromCountItem, toCountItem))
                    // 동일한 CountableItem인 경우 => toSlot에 최대치만큼 채우고, 나머지가 존재하면 fromSlot에 반환
                {
                    int excess = toCountItem.AddAmount(fromCountItem.GetAmount);
                    fromCountItem.SetAmount(excess);
                    return true;
                }
                
                // 각 슬롯에 보관중인 아이템 사본 생성
                var fromSlotItem = fromSlot.GetItem.Clone<Item>();
                var toSlotItem = toSlot.GetItem?.Clone<Item>();

                if (toSlot.HasItem) // toSlot에 아이템이 존재하는 경우 => Swap
                {
                    if (fromSlot.CanStore(toSlotItem?.GetItemInfo))
                    {
                        return toSlot.Store(fromSlotItem) && fromSlot.Store(toSlotItem, true);
                    }

                    return false;
                }

                // toSlot이 비어있는 경우 => 단순 이동
                return toSlot.Store(fromSlotItem) && fromSlot.Clear(); // toSlot에 fromSlot의 Item 저장 성공 + fromSlot 비우기
            }
            finally // fromSlot과 toSlot의 변동 알림
            {
                NotifySlotUpdated(fromSlot);
                NotifySlotUpdated(toSlot);
            }
        }

        private void OverwriteSlot(ItemSlot fromSlot, ItemSlot toSlot, bool emptyPrevSlot = false)
        {
            if (fromSlot.GetItem.Clone<Item>() is not { } fromSlotItem) return;
            
            toSlot.Store(fromSlotItem, true);
            if (emptyPrevSlot) fromSlot.Clear();
        }

        private void SwapSlot(int oneIdx, int anotherIdx)
        {
            if (oneIdx == anotherIdx || !IsValidInventorySlot(oneIdx) || !IsValidInventorySlot(anotherIdx)) return;

            var one = inventoryItems[oneIdx];
            var another = inventoryItems[anotherIdx];

            inventoryItems[oneIdx] = another;
            inventoryItems[oneIdx].Index = oneIdx;
            inventoryItems[anotherIdx] = one;
            inventoryItems[anotherIdx].Index = anotherIdx;
        }
        
        private bool Consume(Item item) // only for CountableItem
        {
            if (item is not CountableItem countItem || !countItem.GetItemInfo.isUsable) return false;
            
            //todo: 사용효과 구현
            
            int oneLess = countItem.GetAmount - 1;
            countItem.SetAmount(oneLess);

            var itemInfo = countItem.GetItemInfo;
            if (countableDict.TryGetValue(itemInfo, out var total))
            {
                countableDict[itemInfo] = total - 1;
            }
            
            return true;
        }

        private void UseItem(ItemSlot targetSlot) // 아이템 사용 (장착, 소비, 등)
        {
            if (targetSlot.GetItemInfo is not { isUsable: true }) return; // 사용 가능한(소비, 장착) 아이템이 아닌 경우

            if (targetSlot is EquipmentSlot { } equipmentSlot // 해당 슬롯이 장비 슬롯(장착 목적)인 경우
                && FindEmptySlotIndex() is {} emptySlotIndex and > 0) // and 인벤토리에 빈 슬롯이 있는 경우
            {
                TransferItem(equipmentSlot, inventoryItems[emptySlotIndex]); // 장비칸의 장비를 빈 슬롯으로 이동
            }
            else if (targetSlot.GetItem is EquipmentItem equipment) // 인벤토리에 보관된 장비 아이템인 경우
            {
                if (FindSuitableEquipSlot(equipment) is { } equipSlot)
                {
                    TransferItem(targetSlot, equipSlot);
                }
            }
            else // 소비 아이템인 경우 (Consume)
            {
                if (this.Consume(targetSlot.GetItem))
                {
                    NotifySlotUpdated(targetSlot);
                }
            }
        }

        public void DivideItem(ItemSlotUI slotUI)
        {
            DivideItem(FindUITargetSlot(slotUI));
        }
        
        public void DivideItem(ItemSlot slot, int amount = -1) // 아이템 개수 분리, CountableItem만 지원, amount: -1 -> 절반으로 분리
        {
            if (slot?.GetAmount <= 1) {
                Util.Log($"[{nameof(InventorySystem)}.{nameof(DivideItem)}()] not enough amount");
                return; // 대상 슬롯의 아이템 개수가 1 이하이면 취소
            }
            if (FindEmptySlotIndex() is not ({} foundIdx and >= 0)) {
                Util.Log($"[{nameof(InventorySystem)}.{nameof(DivideItem)}()] no empty slot");
                return; // 빈 슬롯이 없으면 취소
            }

            if (inventoryItems[foundIdx] is not { } foundSlot || ReferenceEquals(slot, foundSlot))
            {
                Util.Log($"[{nameof(InventorySystem)}.{nameof(DivideItem)}()] found slot is same with origin slot");
                return;
            }
            
            switch (slot)
            {
                // 슬롯 내부의 아이템 인스턴스가 CountableItem인 경우
                case { GetItem: CountableItem countableItem }:
                {
                    int split = amount < 0 ? (countableItem.GetAmount / 2) : amount;
                    if (countableItem.SeparateAndClone<CountableItem>(split) is not { } divided) return;
                    // 빈 슬롯에 개수 분리한 아이템 저장 시도
                    if (foundSlot.Store(divided, false, currFilter))
                    {
                        // 저장 성공 시 슬롯 내부 데이터 변경을 알림
                        NotifySlotUpdated(slot);
                        NotifySlotUpdated(foundSlot);
                    }
                    else
                    {
                        countableItem.AddAmount(split); // 저장 실패 시 원복
                    }
                    break;
                }
                // 슬롯 내부의 아이템 인스턴스가 CountableItem이 아니지만 아이템 데이터가 Countable인 경우
                case { GetItemInfo: { itemType: Enums.ItemType.Countable }, GetItem: not CountableItem }:
                    if (ModifyItemInstanceByType(slot.GetItem) is CountableItem cItem) // 아이템 타입에 적합한 인스턴스로 재생성
                    {
                        int split = amount < 0 ? (cItem.GetAmount / 2) : amount;
                        if (cItem.SeparateAndClone<CountableItem>(split) is not { } divided) return;
                        // 빈 슬롯에 개수 분리한 아이템 저장 시도 + 재생성된 인스턴스로 슬롯에 새로 저장
                        if (foundSlot.Store(divided, false, currFilter) &&
                            slot.Store(cItem, true, currFilter))
                        {
                            // 저장 성공 시 슬롯 내부 데이터 변경을 알림
                            NotifySlotUpdated(slot);
                            NotifySlotUpdated(foundSlot); 
                        }
                        else
                        {
                            cItem.AddAmount(split); // 저장 실패 시 원복
                        }
                    }
                    break;
                default:
                    Util.Log($"[{nameof(InventorySystem)}.{nameof(DivideItem)}()] not supported item Type. {slot?.GetItemInfo}");
                    break;
            }
        }
        
        #endregion
        
        #region Compare Items
        
        public enum ItemComparator
        {
            Exact, // 정확히 동일한 아이템인지 (ItemTypeSO 기준)
            Type, // 동일한 타입인지 (Enums.ItemType 기준)
            EquipmentType, // 장비 대분류가 동일한지 (Enums.EquipmentType 기준)
            EquipSlotType, // 장착 가능한 슬롯 종류가 동일한지 (Enums.EquippedSlotType 기준)
            SpecifiedEquipmentType, // 동일 장비군인지
        }

        public bool CompareItems(Item a, Item b, ItemComparator comparator)
        {

            return comparator switch
            {
                ItemComparator.Exact => IsSameItem(a, b),
                ItemComparator.Type => IsSameType(a, b),
                ItemComparator.EquipmentType => IsSameEquipmentType(a, b),
                ItemComparator.EquipSlotType => IsSameEquipSlotType(a, b),
                _ => false // todo: 나머지 ItemComparator 대응 매서드 추가
            };
        }

        private bool IsSameItem(Item a, Item b) // 정확히 동일한 아이템인지 검사 (ItemTypeSO 기준)
        {
            return (a?.GetItemInfo == b?.GetItemInfo);
        }

        private bool IsSameType(Item a, Item b) // 동일한 타입인지 검사 (Enums.ItemType 기준)
        {
            return (a?.GetItemInfo.itemType == b?.GetItemInfo.itemType);
        }

        private bool IsSameEquipmentType(Item a, Item b) // 장비 대분류가 동일한지 검사 (Enums.EquipmentType 기준)
        {
            if (a?.GetItemInfo is EquipmentTypeSO aData && b?.GetItemInfo is EquipmentTypeSO bData)
            {
                return aData.equipmentType == bData.equipmentType;
            }
            
            return false;
        }

        private bool IsSameEquipSlotType(Item a, Item b) // 장착 슬롯 종류가 동일한지 검사 (Enums.EquippedSlotType 기준)
        {
            if (a?.GetItemInfo is EquipmentTypeSO aData && b?.GetItemInfo is EquipmentTypeSO bData)
            {
                return aData.slotType == bData.slotType;
            }
            
            return false;
        }

        // private bool IsSameSpecifiedEquipment(Item a, Item b) // 동일 무기군인지 검사 (미구현)
        // {
        //     return false;
        // }

        #endregion
        
        #region UI Interaction

        public void TrySwapItems(ItemSlotBaseUI fromSlotUI, ItemSlotBaseUI toSlotUI) // 아이템 드래그&드랍
        {
            // UI_ItemSlotBase를 가지는 다른 오브젝트(ex-창고)가 생길 경우, FindUITargetSlot()이 제대로 작동하지 않을 수 있음
            // todo: 인벤토리 외 아이템 보관을 포함하는 기능이 추가될 경우 매서드 확장 혹은 기능 이전 필요
            var fromSlot = FindUITargetSlot(fromSlotUI);
            var toSlot = FindUITargetSlot(toSlotUI);
            
            TransferItem(fromSlot, toSlot);
        }

        public void TryUseItem(ItemSlotBaseUI targetSlotUI)
        {
            var targetSlot = FindUITargetSlot(targetSlotUI);
            UseItem(targetSlot);
        }

        public void TryFilterInven(InventoryFilterType filter)
        {
            if (currFilter == filter) return;
            FilterInven(filter);
        }

        #endregion

        #region Notify/Listen Event

        private void NotifySlotUpdated(ItemSlot slot) // (Equipment, Inventory) 특정 슬롯의 변동 발생을 알림
        {
            if (slot is EquipmentSlot equipmentSlot)
            {
                OnEquippedSlotChanged?.Invoke(equipmentSlot.Index);
            }
            else
            {
                OnInventorySlotChanged?.Invoke(slot.Index);
            }
        }
        
        private void OnEquipmentChanged(object sender, EquipmentSlotArgs args) // 장비 아이템의 변동사항(장착/해제) 처리
        {
            if (args.State == EquipmentSlotArgs.EquipEventState.Equip && args.Item.GetItemInfo.itemType == Enums.ItemType.Equipment)
            {
                if (args.Item.GetItemInfo is not WeaponTypeSO weaponTypeSO) return; // todo: 무기 이외 타입 처리 추가
                
                if (GameObject.FindWithTag("Player") is { } player &&
                    player.GetComponent<Fighter>() is { } pFighter)
                {
                    pFighter.EquipWeapon(weaponTypeSO); // 플레이어 캐릭터에게 장비 착용
                }
            }
            else // case: args.State == EquipmentSlotArgs.EquipEventState.UnEquip)
                 // or 장비가 아닌 아이템 (빈 아이템)을 장착하려 한 경우 -> 장착해제
            {
                if (GameObject.FindWithTag("Player") is { } player &&
                    player.GetComponent<Fighter>() is { } pFighter)
                {
                    pFighter.UnEquipWeapon(); // 플레이어 캐릭터의 장비 착용 해제
                }
            }

            if (sender is ItemSlot changedSlot) 
                NotifySlotUpdated(changedSlot); // 해당 장비 슬롯의 변동 알림 (인벤토리 UI 등에 동기화 목적)
        }

        #endregion
        
        #region Item/Slot Validation

        public Item ModifyItemInstanceByType(Item item) 
        {
            // 파일, 프리팹 등의 데이터에서 받아온 Item 인스턴스를 아이템타입 데이터에 맞는 Item 상속 클래스 인스턴스로 변환
            switch (item.GetItemInfo.itemType)
            {
                case Enums.ItemType.Countable:
                    if (item is CountableItem) return item;
                    return new CountableItem(item.GetItemInfo, item.GetAmount);
                case Enums.ItemType.Equipment:
                    if (item is EquipmentItem) return item;
                    return new EquipmentItem(item.GetItemInfo);
                default:
                    // todo: 필요한 경우 Single, Special 등 아이템 타입 구현 
                    return item;
            }
        }
        
        private ItemSlot MakeEmptyItemSlot(int index = -1, ItemSlotValidationStandard validation = ItemSlotValidationStandard.All)
        {
            return validation switch
            {
                ItemSlotValidationStandard.All => new ItemSlot(null, index, inventoryValidItemTypes),
                _ => new ItemSlot(null, index, inventoryValidItemTypes)
            };
        }
        
        private enum ItemSlotValidationStandard // 슬롯에 들어갈 수 있는 아이템 타입 기준 (타입별 readonly 배열 추가해서 사용)
        {
            All, // inventoryValidItemTypes
            // todo 추가
        }
        
        private readonly Enums.ItemType[] inventoryValidItemTypes = // 인벤토리 슬롯: 모든 아이템 가능
        {
            Enums.ItemType.Countable,
            Enums.ItemType.Special,
            Enums.ItemType.Equipment,
            Enums.ItemType.Single,
        };

        private readonly Enums.ItemType[] equippedValidItemTypes = // 장착 장비 슬롯: 장비(EquipmentItem)만 가능
        {
            Enums.ItemType.Equipment,
        };

        #endregion

        #region Save/Load (ISavable)
        
        public object CaptureState()
        {
            List<Item> invenItems = new();

            foreach (var itemSlot in inventoryItems)
            {
                if (itemSlot is { HasItem: true })
                {
                    invenItems.Add(itemSlot.GetItem.Clone<Item>());
                }
            }

            return invenItems;
        }

        public bool RestoreState(object state)
        {
            foreach (var itemSlot in inventoryItems)
            {
                itemSlot?.Clear();
            }
            
            List<Item> loadedInvenItems = (List<Item>)state;

            foreach (var item in loadedInvenItems)
            {
                AddItem(item, checkInstanceType: true);
            }

            return true;
        }

        #endregion

        #region Sort Test
        
        private readonly InventorySlotComparer itemSortingComparer = new();
        private sealed class InventorySlotComparer : IComparer<ItemSlot>
        {
            public int Compare(ItemSlot x, ItemSlot y)
            {
                // null, 빈 슬롯 뒤로
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return 1;
                if (y is null) return -1;
                
                bool xe = !x.HasItem;
                bool ye = !y.HasItem;
                if (xe && ye) return 0;
                if (xe) return 1;
                if (ye) return -1;
                
                var xi = x.GetItemInfo;
                var yi = y.GetItemInfo;
                var xt = xi?.itemType;
                var yt = yi?.itemType;
                
                // 1) 아이템 타입명 
                string xTypeName = xt?.ToString() ?? string.Empty;
                string yTypeName = yt?.ToString() ?? string.Empty;
                int c = string.Compare(xTypeName, yTypeName, StringComparison.Ordinal);
                if (c != 0) return c;

                // 2) 아이템 이름
                string xItemName = xi?.nameString ?? string.Empty;
                string yItemName = yi?.nameString ?? string.Empty;
                c = string.Compare(xItemName, yItemName, StringComparison.Ordinal);
                if (c != 0) return c;

                // 3) 정렬 전 위치 기준
                return x.Index.CompareTo(y.Index); // 정렬 이후에는 반드시 Index 갱신 필요
            }
        }

        #endregion
    }

    public static class InventorySystemExtensionMethods
    {
        public static bool Store(this ItemSlot slot, Item item, bool byForce, InventorySystem.InventoryFilterType filter)
        {
            if (!slot.Store(item, byForce)) return false;
            
            slot.SetVisibility(InventorySystem.IsVisibleByFilter(slot, filter));
            return true;
        }
    }
}




using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Saving;
using UnityEngine;

namespace RPG.Item
{
    public class InventorySystem: MonoBehaviour, ISavable
    {
        public int Capacity { get; private set; }
        [SerializeField, Range(8, 256)] private int initialCapacity = 64; //실제론 inspector 값이 들어가니 주의 //todo: Constants에서 선언하고 사용할지 고민
     
        // item data container (itemSlot)
        private ItemSlot[] inventoryItems; // 인벤토리에 보관된 아이템 목록
        private EquipmentSlot[] equippedItems; // 장착 중인 장비(무기, 방어구) 목록 // todo: 장착된 장비 능력치 반영
        
        // itemSlot Delegate
        // public event Action OnInventoryChanged; // 인벤토리 전체 초기화가 필요한 경우
        // public event Action OnEquippedChanged; // 장착 슬롯 전체 초기화가 필요한 경우
        public event Action<int> OnInventorySlotChanged; // 1개의 인벤토리 슬롯 초기화가 필요한 경우 (인덱스 접근)
        public event Action<int> OnEquippedSlotChanged; // 1개의 장비 슬롯 초기화가 필요한 경우 (인덱스 접근)
        
        private void Awake()
        {
            Capacity = initialCapacity;
            inventoryItems = new ItemSlot[initialCapacity];
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
            
            ResourceManager.Instance.SubscribePreLoad((_) =>
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
                 Util.Log($"Trying to add {item}");
                 AddItem(item, checkInstanceType: true);
             }
            });
        }

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

        public bool CanStore(UI_ItemSlotBase fromSlotUI, UI_ItemSlotBase toSlotUI)
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
        
        public ItemSlot FindUITargetSlot(UI_ItemSlotBase slotUI)
        {
            if (slotUI is UI_EquipmentSlot)
            {
                Util.Log($"[FindUITargetSlot] UI_EquipmentSlot index:{slotUI.Index}");
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

        #region Add/Remove/Transfer/Consume Item(Inventory)
        
        public int AddItem(Item item, int amount = 1, bool checkInstanceType = false) // 인벤토리 슬롯에 아이템 추가, CountableItem
        {
            if (checkInstanceType)
            {
                item = ModifyItemInstanceByType(item);
            }
            
            int index;
            if ((!item?.IsValid) ?? true) return 0;
            if (item is CountableItem countItem)
            {
                bool findNextCountable = true;
                index = -1;
                amount *= countItem.GetAmount;
                
                while (amount > 0)
                {
                    if (findNextCountable)
                    {
                        index = FindCountableItemSlotIndex(countItem, index + 1);
                        if (index == -1)
                        {
                            findNextCountable = false;
                        }
                        else
                        {
                            // 개수 추가 및 슬롯 최대개수 초과량을 반환 (개수 추가 실패시 0 반환)
                            amount = (inventoryItems[index].GetItem as CountableItem)?.AddAmount(amount) ?? 0;
                            OnInventorySlotChanged?.Invoke(index);
                        }
                    }
                    else // 한도수량에 도달하지 않은 동일 아이템이 존재하지 않는 경우, 빈 슬롯 탐색
                    {
                        index = FindEmptySlotIndex(index + 1);
                        if (index == -1)
                        {
                            return amount; // 빈 슬롯이 없는 경우, 남은 개수 반환
                        }

                        int maxAmount = countItem.GetItemInfo.maxAmount;
                        if (countItem.GetAmount > maxAmount)
                        {
                            countItem.SetAmount(maxAmount);
                            if (inventoryItems[index].Store(countItem))
                            {
                                amount -= maxAmount; // 슬롯에 아이템 저장 성공시, 저장한 개수만큼 차감
                            }
                            else
                            {
                                return amount; // 실패시 즉시 남은 개수 반환
                            }
                        }
                        else
                        {
                            countItem.SetAmount(amount);
                            if (inventoryItems[index].Store(countItem))
                            {
                                return 0;
                            }
                            else
                            {
                                return amount;
                            }
                        }
                    }
                }
            }
            else // 수량이 없는 아이템
            {
                index = -1;
                while (amount > 0)
                {
                    index = FindEmptySlotIndex(index + 1);
                    if (index == -1) break; // 빈칸을 찾지 못한 경우, 루프 탈출

                    if (inventoryItems[index].Store(item.Clone<Item>())) // 빈칸에 아이템 저장 성공 시 
                    {
                        amount--; // 남은 개수 -1
                        NotifySlotUpdated(inventoryItems[index]); // 해당 슬롯 데이터 변동 알림
                    }
                    else // 아이템 저장 실패 시
                    {
                        return amount; // 남은 개수 즉시 반환
                    }
                }
            }

            return amount;
        }
        
        public bool RemoveItem(int index) // 해당 인덱스 위치 슬롯의 아이템 제거 (슬롯 비우기)
        {
            if (!IsValidInventorySlot(index)) return false;
            if (!inventoryItems[index].IsAccessible) return false;
            if (inventoryItems[index].GetItemInfo.itemType == Enums.ItemType.Special) return false;

            return inventoryItems[index].Clear();
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
        
        private bool Consume(Item item)
        {
            Util.Log($"ConsumeItem is not ready");
            return false;
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

        public void TrySwapItems(UI_ItemSlotBase fromSlotUI, UI_ItemSlotBase toSlotUI) // 아이템 드래그&드랍
        {
            // UI_ItemSlotBase를 가지는 다른 오브젝트(ex-창고)가 생길 경우, FindUITargetSlot()이 제대로 작동하지 않을 수 있음
            // todo: 인벤토리 외 아이템 보관을 포함하는 기능이 추가될 경우 매서드 확장 혹은 기능 이전 필요
            var fromSlot = FindUITargetSlot(fromSlotUI);
            var toSlot = FindUITargetSlot(toSlotUI);
            
            TransferItem(fromSlot, toSlot);
        }

        public void TryUseItem(UI_ItemSlotBase targetSlotUI)
        {
            var targetSlot = FindUITargetSlot(targetSlotUI);
            if (targetSlot.GetItemInfo is not { isUsable: true }) return;

            if (targetSlot.GetItem is EquipmentItem equipment)
            {
                if (FindSuitableEquipSlot(equipment) is { } equipSlot)
                {
                    TransferItem(targetSlot, equipSlot);
                }
            }
            else
            {
                if (this.Consume(targetSlot.GetItem))
                {
                    NotifySlotUpdated(targetSlot);
                }
            }
        }

        #endregion

        #region Notify/Listen Event

        private void NotifySlotUpdated(ItemSlot slot)
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
        
        private void OnEquipmentChanged(object sender, EquipmentSlotArgs args)
        {
            if (args.State == EquipmentSlotArgs.EquipEventState.Equip)
            {
                if (args.Item.GetItemInfo is WeaponTypeSO weaponTypeSO)
                {
                    var player = GameObject.FindWithTag("Player");
                    var playerFighter = player.GetComponent<Fighter>();
                    if (playerFighter == null) return;
                    
                    playerFighter.EquipWeapon(weaponTypeSO);
                }
            }
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
                    invenItems.Add(itemSlot.GetItem);
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
        
    }
}


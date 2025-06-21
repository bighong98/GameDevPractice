using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Item
{
    public class InventorySystem : MonoBehaviour
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
            
            ResourceManager.Instance.SubscribePreLoad((_) =>
            {
                InventoryTestData testData =
                    ResourceManager.Instance.Load<GameObject>("InventoryTestData.prefab").GetComponent<InventoryTestData>();

                if (testData == null)
                {
                    Util.Log("TestData is null");
                    return;
                }
                
                foreach (var data in testData.itemTypeHolders)
                {
                    Util.Log($"Trying to add {data.type}");
                    AddItem(data.type, data.amount);
                }
            });
        }

        #region Read Slot

        public IEnumerable<ItemSlot> ReadOnlyInventorySlots => inventoryItems;
        public IEnumerable<ItemSlot> ReadOnlyEquippedSlots => equippedItems;
        
        public ItemSlot GetInventorySlotInfo(int index, bool checkAccessibility = false)
        {
            if (checkAccessibility && !IsValidIndex(index))
            {
                Util.Log($"[GetInventorySlotInfo] return null");
                return null;
            }
            
            return inventoryItems[index];
        }

        public ItemSlot GetEquippedSlotInfo(int index, bool checkAccessibility = false)
        {
            if (checkAccessibility && !IsValidIndex(index)) return null;
            return equippedItems[index];
        } 

        private int FindEmptySlotIndex(int start = 0)
        {
            for (int i = start; i < Capacity; i++)
            {
                if (inventoryItems[i] != null) continue;
                return i;
            }

            return -1; // 빈칸이 없으면 -1 반환
        }

        private int FindCountableItemSlotIndex(CountableItemSlot cItemSlot, int start = 0)
        {
            for (int i = start; i < Capacity; i++)
            {
                if (inventoryItems[i] == null) continue;
                if (inventoryItems[i].GetItemInfo.itemType != Enums.ItemType.Countable) continue;
                if (!IsSameItem(inventoryItems[i].GetItemInfo, cItemSlot.GetItemInfo)) continue;

                return i; // 동일한 Countable 타입 아이템을 찾은 경우, 해당 인덱스 반환
            }

            return -1; // 인벤토리에 동일 아이템이 없는 경우 -1 반환 (=실패)
        }

        private bool IsSameItem(ItemTypeSO a, ItemTypeSO b)
        {
            return (a == b);
        }

        public int GetItemAmount(int index)
        {
            if (!IsValidIndex(index)) return 0;
            return inventoryItems[index].GetAmount;
        }
        
        public bool IsValidIndex(int index)
        {
            return (index >= 0 && index < Capacity);
        }

        public bool IsValidIndexForEquipmentSlot(int index) => index is
            >= (int)Enums.EquippedItemSlotType.Weapon
            and
            < (int)Enums.EquippedItemSlotType.Max;

        public bool IsValidSlotForEquipment(UI_ItemSlotBase slotUI, ItemSlot item)
        {
            //todo: 타입 추가 후 구현
            return item switch
            {
                _ => false
            };
        }
        
        public bool HasItem(int index)
        {
            return (IsValidIndex(index) && inventoryItems[index] != null);
        }
        
        #endregion

        #region Update Slot
        
        public int AddItem(ItemTypeSO typeData, int amount)
        {
            switch (typeData.itemType)
            {
                case Enums.ItemType.Countable:
                    return AddItem(new CountableItemSlot(typeData, amount)); // CountableItem을 제외하고 amount는 무시됨 (기본값 1 적용)
                case Enums.ItemType.Equipment:
                    return AddItem(new EquipmentSlot(typeData));
                default:
                    return AddItem(new ItemSlot(typeData));
            }
        }

        public int AddItem(ItemSlot itemSlot, int amount = 1)
        {
            int index;
            if (itemSlot is CountableItemSlot countItem)
            {
                bool findNextCountable = true;
                index = -1;

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
                            amount = (inventoryItems[index] as CountableItemSlot)?.AddAmount(amount) ?? 0; // 개수 추가 및 슬롯당 최대개수 초과량을 반환 (개수 추가 실패시 0 반환)
                            NotifySlotUpdated(inventoryItems[index]);
                        }
                    }
                    else // 한도수량에 도달하지 않은 동일 아이템이 존재하지 않는 경우, 빈 슬롯 탐색
                    {
                        index = FindEmptySlotIndex(index + 1);
                        if (index == -1)    
                            break;
                    
                        inventoryItems[index] = countItem.Clone<CountableItemSlot>(amount, out int excess);
                        amount = excess;
                        
                        NotifySlotUpdated(inventoryItems[index]);
                    }
                }
            }
            else // 수량이 없는 아이템
            {
                index = -1;
                while (amount > 0)
                {
                    index = FindEmptySlotIndex(index + 1);
                    if (index == -1)
                        break;
                
                    inventoryItems[index] = itemSlot.Clone<ItemSlot>();
                    amount--;
                    
                    NotifySlotUpdated(inventoryItems[index]);
                }
            }

            return amount;
        }

        public void RemoveItem(int index)
        {
            if (inventoryItems[index].GetItemInfo.itemType == Enums.ItemType.Special)
            {
                // todo: 스페셜 아이템은 지우지 못하도록 UI팝업으로도 안내
                return;
            }

            inventoryItems[index] = null;
            NotifySlotUpdated(inventoryItems[index]);
        }

        public void TrySwapItems(UI_ItemSlotBase fromSlotUI, UI_ItemSlotBase toSlotUI)
        {
            var fromSlot = GetUITargetSlot(fromSlotUI);
            var toSlot = GetUITargetSlot(toSlotUI);
            
            if (fromSlot is CountableItemSlot fromCountItem &&
                toSlot is CountableItemSlot toCountItem &&
                IsSameItem(fromCountItem.GetItemInfo, toCountItem.GetItemInfo)) 
                // 동일한 CountableItem인 경우, 개수 합치기
            {
                int excess = toCountItem.AddAmount(fromCountItem.GetAmount);
                fromCountItem.SetAmount(excess);
            }
            else
            {
                var fromClone = fromSlot.Clone<ItemSlot>();
                var toClone = toSlot.Clone<ItemSlot>();
                
                //todo: 실제 데이터에 반영
            }
            
            NotifySlotUpdated(fromSlot);
            NotifySlotUpdated(toSlot);
        }
        
        private void SwapItem(ref ItemSlot fromSlot, ref ItemSlot toSlot)
        {
            if (fromSlot is CountableItemSlot fromCountItem &&
                toSlot is CountableItemSlot toCountItem &&
                IsSameItem(fromCountItem.GetItemInfo, toCountItem.GetItemInfo)) 
                // 동일한 CountableItem인 경우, 개수 합치기
            {
                int excess = toCountItem.AddAmount(fromCountItem.GetAmount);
                fromCountItem.SetAmount(excess);
            }
            else // 그 외의 경우 자리 교체
            {
                (fromSlot, toSlot) = (toSlot, fromSlot);
            }
            NotifySlotUpdated(fromSlot);
            NotifySlotUpdated(toSlot);
        }

        #endregion

        private ItemSlot GetUITargetSlot(UI_ItemSlotBase slotUI)
        {
            if (slotUI is UI_EquipmentSlot)
            {
                if (!IsValidIndexForEquipmentSlot(slotUI.Index)) return null;
                return equippedItems[slotUI.Index];
            }
            else
            {
                if (!IsValidIndex(slotUI.Index)) return null;
                return inventoryItems[slotUI.Index];
            }
        }
        
        private void NotifySlotUpdated(ItemSlot slot)
        {
            if (slot is EquipmentSlot equipmentSlot)
            {
                OnEquippedSlotChanged?.Invoke(equipmentSlot.GetIndex);
            }
            else
            {
                OnInventorySlotChanged?.Invoke(slot.GetIndex);
            }
        }
        
    }
}


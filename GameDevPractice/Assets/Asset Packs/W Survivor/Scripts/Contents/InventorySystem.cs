using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Item;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class InventorySystem : MonoBehaviour
{
    public int Capacity { get; private set; }
    [SerializeField, Range(8, 256)] private int _initialCapacity = 64; //실제론 inspector 값이 들어가니 주의 //todo: Constants에서 선언하고 사용할지 고민
    [SerializeField] private UI_Inventory _inventoryPopup;
    
    private BaseItem[] _items; // 인벤토리에 저장된 아이템
    private EquipmentItem[] _equipments; // 장착 중인 장비 (아이템)

    private UseItemSystem _useItemSystem = new UseItemSystem();
    
    private void Awake()
    {
        _items = new BaseItem[_initialCapacity];
        Capacity = _initialCapacity;
        _equipments = new EquipmentItem[(int)Enums.EquippedItemSlotType.Max];
        _useItemSystem.Init();
    }

    private void Start()
    {
        // _inventoryPopup = UIManager.Instance.ShowPopupUI<UI_Inventory>().InitImmediately();
        // _inventoryPopup.ClosePopupUI();
        // AddItem(InGameManager.Instance.ItemSpawner.ExtractItemData(2));
        // AddItem(InGameManager.Instance.ItemSpawner.ExtractItemData(3));
    }
    
    //todo: 모든 슬롯 UI에 접근 가능 여부 업데이트? 현재 접근 가능한 슬롯의 한계치 검사?
    
    #region Read Information
    
    public BaseItem GetItem(int index) => _items[index];
    public ref BaseItem GetItemRef(int index) => ref _items[index];
    public EquipmentItem GetEquipment(int index) => _equipments[index];
    public ref EquipmentItem GetEquipmentRef(int index) => ref _equipments[index];
    
    private int FindEmptySlotIndex(int startIndex = 0)
    {
        for (int i = startIndex; i < Capacity; i++)
            if (_items[i] == null)
                return i;
        
        return -1; // -1 means failure
    }

    private int FindCountableItemSlotIndex(CountableItem item, int startIndex = 0)
    {
        int itemId = item.ItemId;
        for (int i = startIndex; i < Capacity; i++)
        {
            if (_items[i] is CountableItem countableItem && countableItem.ItemId == itemId && !countableItem.IsMax)
                return i; 
        }
        return -1; // -1 means failure
    }
    
    public int GetCurrentAmount(int index)
    {
        if (!IsValidIndex(index)) return -1; // -1 means invalid index
        if (_items[index] == null) return 0; // 0 means empty
        
        return ((_items[index] as CountableItem)?.Amount ?? 1);
    }
    public bool IsValidIndex(int index)
    {
        return (index >= 0 && index < Capacity);
    }
    
    public bool HasItem(int index)
    {
        return (IsValidIndex(index) && _items[index] != null);
    }

    public bool IsCountable(int index)
    {
        return (HasItem(index) && (_items[index] is CountableItem));
    }

    #endregion

    public void UpdateSlot(int index)
    {
        if (!IsValidIndex(index))
        {
            Util.Log("Trying Update Slot in Invalid index");
            return;
        }
        
        BaseItem item = _items[index]; //Util.Log($"UpdateSlot: index: {index}, item.id: {item.ItemId}, item.name: {item.ItemName}, item.sprite: {item.ItemSpriteName}, item.amount: {(item as CountableItem)?.Amount}");
        
        if (item != null) // 1. 슬롯에 아이템이 존재하는 경우
        {
            _inventoryPopup.SetSlotIcon(index, item);
            if (item is CountableItem cItem) // 1-1. 셀 수 있는 아이템 
            {
                if (cItem.IsEmpty) // 1-1-a. 개수가 0: 슬롯 초기화
                {
                    _items[index] = null;
                    _inventoryPopup.CleanSlot(index);
                    return;
                }
                else // 1-1-b. 개수가 1이상: 개수 반영
                {
                    _inventoryPopup.SetSlotItemAmount(index, cItem.Amount);
                    _inventoryPopup.ShowSlotItemAmountText(index);
                }
            }
            else // 1-2. 셀 수 없는 아이템: 수량 텍스트 제거 
            {
                _inventoryPopup.HideSlotItemAmountText(index);
            }
        }
        else // 2. 슬롯에 아이템이 없는 경우: 슬롯 초기화
            _inventoryPopup.CleanSlot(index);
    }
    
    public int AddItem(BaseItem item, int amount = 1)
    {
        int index;

        if (item is CountableItem countItem)
        {
            bool findNextCountable = true;
            index = -1;

            while (amount > 0)
            {
                if (findNextCountable)
                {
                    index = FindCountableItemSlotIndex(countItem, index + 1);
                    if (index == -1)
                        findNextCountable = false;
                    else
                    {
                        amount = (_items[index] as CountableItem)?.AddAmount(amount) ?? 0; // 최대치 초과량을 반환, 개수 수정 오류 발생시 0 반환
                        UpdateSlot(index);
                    }
                }
                else // 한도수량에 도달하지 않은 동일 아이템이 존재하지 않는 경우, 빈 슬롯 탐색
                {
                    index = FindEmptySlotIndex(index + 1);
                    if (index == -1)    
                        break;
                    
                    _items[index] = countItem.Clone<CountableItem>(amount, out int excess);
                    amount = excess;
                    
                    UpdateSlot(index);

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
                
                _items[index] = item.Clone<BaseItem>();
                amount--;
                UpdateSlot(index);
            }
        }

        return amount;
    }
    
    public void RemoveInventoryItem(int index)
    {
        if (_items[index] is SpecialItem)
        {
            // todo: 스페셜 아이템은 지우지 못하도록 UI팝업으로도 안내
            return;
        }
        
        _items[index] = null;
        UpdateSlot(index);
    }

    public void SwapItem(int fromIndex, int toIndex) // 인벤토리 내에서 아이템 슬롯 교환
    {
        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex)) return;

        BaseItem fromItem = _items[fromIndex];
        BaseItem toItem = _items[toIndex];
        
        if (fromItem != null && toItem != null &&
            fromItem.ItemId == toItem.ItemId &&
            fromItem is CountableItem fromItemCountable &&
            toItem is CountableItem toItemCountable)
        { // 동일한 CountableItem인 경우: toIndex 슬롯에 합치기 시도
            int max = toItemCountable.MaxAmount;
            int sum = fromItemCountable.Amount + toItemCountable.Amount;

            if (sum <= max)
            {
                fromItemCountable.SetAmount(0);
                toItemCountable.SetAmount(sum);
            }
            else
            {
                fromItemCountable.SetAmount(sum - max);
                toItemCountable.SetAmount(max);
            }
        }
        else // 일반적인 경우: 슬롯 교체
        {
            _items[fromIndex] = toItem;
            _items[toIndex] = fromItem;
        }
        UpdateSlot(fromIndex);
        UpdateSlot(toIndex);
    }
    
    public BaseItem GetItemInSlot(UI_ItemSlotBase slot) // 반드시 UI_Inventory와 InventorySystem의 상태가 동기화되어있어야 함
    {
        return slot switch
        {
            UI_ItemSlot inventorySlot => GetItem(inventorySlot.Index),
            UI_EquipmentSlot equipmentSlot => GetEquipment(equipmentSlot.Index),
            _ => null
        };
    }
    
    public bool ReplaceItemInSlot(UI_ItemSlotBase currSlot, BaseItem nextItem)
    {
        switch (currSlot)
        {
            case UI_ItemSlot inventorySlot:
                ref BaseItem itemRef = ref GetItemRef(inventorySlot.Index);
                itemRef = nextItem;
                UpdateSlot(inventorySlot.Index);
                return true;
            case UI_EquipmentSlot equipmentSlot when nextItem is EquipmentItem equipmentItem:
                if (IsValidSlotForEquipment(equipmentSlot, equipmentItem) == false)
                    goto default;
                ref EquipmentItem equipmentRef = ref GetEquipmentRef(equipmentSlot.Index);
                if (!(equipmentRef?.UnEquip() ?? true)) // !(장비 옵션 해제 성공 or 빈칸인 경우 true)
                    goto default;
                equipmentRef = equipmentItem;
                equipmentRef.Equip();
                UpdateEquipmentSlot(equipmentSlot.Index);
                return true;
            default:
                AlertFail();
                return false;
        }

        void AlertFail() => Util.Log("UI_Inventory: Failed to Replace Item In Slot");
    }

    public void TryUseOrEquipItem(int index)
    {
        if (_items[index] == null) 
            return;
        
        switch (_items[index].ItemType)
        {
            case (int)Enums.ItemType.Equipment:
                EquipItem(index);
                break;
            case (int)Enums.ItemType.Countable:
            case (int)Enums.ItemType.Single:
            case (int)Enums.ItemType.Special:
                UseItem(index);
                break;
            default:
                Util.Log("Trying to use Invalid Item type");
                break;
        }
    }

    public void UseItem(int index)
    {
        if (!_items[index].IsUsable) // 사용 가능한 아이템이 아닌 경우 return
            return;

        if (_useItemSystem.UseItem(_items[index].ItemOptionGroup)) // 아이템 사용에 성공했는지를 확인
        {
            switch (_items[index])
            {
                case CountableItem countableItem:
                    countableItem.SetAmount(countableItem.Amount - 1);
                    break;
                case SpecialItem specialItem:
                    break;
                case SingleItem singleItem:
                    RemoveInventoryItem(index);
                    break;
                default:
                    Util.Log("InventorySystem.UseItem: invalid itemType");
                    break;
            }
        }
        UpdateSlot(index);
    }

    public void DivideItem(int index, int amount = 1)
    {
        if (_items[index] is CountableItem cItem)
        {
            int emptyIdx = FindEmptySlotIndex();
            if (emptyIdx == -1)
            {
                //todo: 유저에게 공간이 없음을 알림
                Util.Log("Try DivideItem: No more empty slot");
                return;
            }

            _items[emptyIdx] = cItem.SeparateAndClone<CountableItem>(amount);
            
            UpdateSlot(index);
            UpdateSlot(emptyIdx);
        }
        else
        {   
            Util.Log("Try DivideItem: Not Countable Item");
        }
    }
    
    #region Equipment Slot

    private void UpdateEquipmentSlot(int index)
    {
        if (!IsValidIndexForEquipmentSlot(index))
        {
            Util.Log("InventorySystem: Trying Update Equipment Slot in Invalid Index");
            return;
        }

        if (_equipments[index] is { } equipmentItem) // is not null
            _inventoryPopup.SetEquipmentSlotIcon(index, equipmentItem);
        else
            _inventoryPopup.CleanEquipmentSlot(index);
    }

    private bool IsValidIndexForEquipmentSlot(int index) => index is
        >= (int)Enums.EquippedItemSlotType.Weapon
        and
        < (int)Enums.EquippedItemSlotType.Max;

    public bool IsValidSlotForEquipment(UI_ItemSlotBase slot, BaseItem item)
    {
        // 같은 타입의 슬롯이 여러개인 경우 대응 불가능 (ex: 무기 슬롯 2개)
        return item switch
        {
            WeaponItem weapon => slot.Index == (int)Enums.EquippedItemSlotType.Weapon,
            ArmorItem armor => slot.Index == armor.ArmorType + (int)Enums.EquippedItemSlotType.Head,
            _ => false
        };
    }

    public bool EquipItem(int index)
    {
        int eqSlotIdx; // 장착 시도하려는 장비 슬롯 인덱스
        switch (_items[index])
        {
            case WeaponItem weaponItem:
                eqSlotIdx = (int)Enums.EquippedItemSlotType.Weapon;
                if (_equipments[eqSlotIdx] == null)
                {
                    _equipments[eqSlotIdx] = weaponItem;
                    _items[index] = null;
                }
                else
                {
                    EquipmentItem itemInSlot = _equipments[eqSlotIdx];
                    _equipments[eqSlotIdx] = weaponItem;
                    _items[index] = itemInSlot;
                }
                break;
            case ArmorItem armorItem:
                eqSlotIdx = armorItem.ArmorType + (int)Enums.EquippedItemSlotType.Head;
                if (_equipments[eqSlotIdx] == null) //해당 칸에 아무것도 없으면, 장비칸에 아이템 이전, 인벤토리에서 해당 아이템 제거
                {
                    _equipments[eqSlotIdx] = armorItem;
                    _items[index] = null;
                }
                else //해당 칸에 아이템이 있다면, 장비칸과 장착하려는 아이템의 위치 교환
                {
                    EquipmentItem itemInSlot = _equipments[eqSlotIdx];
                    _equipments[eqSlotIdx] = armorItem;
                    _items[index] = itemInSlot;
                }
                break;
            default:
                Util.Log("InventorySystem: Trying to Equip Invalid Item");
                return false;
        }
        
        UpdateSlot(index);
        _equipments[eqSlotIdx].Equip();
        UpdateEquipmentSlot(eqSlotIdx);
        return true;
    }

    public bool UnEquipItem(int index) // 장비 슬롯의 아이템을 장착 해제
    {
        if (!IsValidIndexForEquipmentSlot(index))
        {
            Util.Log("InventorySystem: Trying to UnEquipItem with invalid index");
            return false;
        }
        
        if (AddItem(_equipments[index]) >= 1) // 인벤토리에 아이템 추가. 추가된 슬롯 업데이트
        {
            Util.Log("InventorySystem: InventorySystem: UnEquipItem failed. No Empty Slot");
            return false;
        }
        
        if (!_equipments[index].UnEquip())
        {
            Util.Log("InventorySystem: InventorySystem: UnEquipItem failed. Modifying player Stat failed");
            return false;
        }
        
        _equipments[index] = null; // 장비 슬롯에서 아이템 제거
        UpdateEquipmentSlot(index); // 장비슬롯 업데이트
        return true;
    }
    
    #endregion

    #region General Slot Function // 종류 무관 아이템 슬롯 관련 함수

    public bool RemoveItem(UI_ItemSlotBase slot)
    {
        switch (slot)
        {
            case UI_ItemSlot inventorySlot:
                _items[inventorySlot.Index] = null;
                UpdateSlot(inventorySlot.Index);
                return true;
            case UI_EquipmentSlot equipmentSlot:
                if (!(_equipments[equipmentSlot.Index]?.UnEquip() ?? true))
                    return false;
                _equipments[equipmentSlot.Index] = null;
                UpdateEquipmentSlot(equipmentSlot.Index);
                return true;
            default:
                return false;
        }
    }

    #endregion
}

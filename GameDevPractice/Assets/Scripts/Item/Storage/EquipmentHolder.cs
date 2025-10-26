using System;
using System.Collections.Generic;
using RPG.Saving;
using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public class EquipmentHolder : MonoBehaviour, IEquipmentHolder, ITypeDependent
    {
        public event EventHandler<EquipArgs> OnEquipmentChanged;
        public event Action<IGameItemSlot> OnSlotChanged;
        public event Action OnStorageChanged;

        public IReadOnlyCollection<IGameItemSlot> ItemSlots => equipments;
        private readonly IGameItemSlot[] equipments = new IGameItemSlot[DefaultSlotNums];

        public int Capacity => DefaultSlotNums;
        private const int DefaultSlotNums = (int)Enums.EquippedItemSlotType.Max;

        
        
        private void Awake()
        {
            FillEquipmentSlots();
            //todo: 저장된 착용 장비가 있다면 장착 -> ISavable
        }

        private void Start()
        {
            OnStorageChanged?.Invoke(); // todo: 기본 장비, 저장 장비 착용 로직 추가 후 호출 시점 조정
        }

        #region Initialization

        private void FillEquipmentSlots()
        {
            //todo: 테이블로 슬롯별 타입 지정
            var slotTypes = Enum.GetValues(typeof(Enums.EquippedItemSlotType));
            for (int i = 0; i < Mathf.Min(equipments.Length, slotTypes.Length); i++)
            {
                equipments[i] = new EquipmentSlot(i, (Enums.EquippedItemSlotType)slotTypes.GetValue(i));
            }
        }

        #endregion
        
        #region Get(Item, Slot)
        
        public bool TryGetItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) && equipments[index] is 
                    { IsAccessible: true, HasItem: true, GetItem: {} stored })
            {
                item = stored;
                return true;
            }

            item = null;
            return false;
        }

        public bool TryGetItemSlot(int index, out IGameItemSlot itemSlot)
        {
            if (IsValidSlotIdx(index) && equipments[index] is { IsAccessible: true } slot)
            {
                itemSlot = slot;
                return true;
            }

            itemSlot = null;
            return false;
        }
        
        // 장착 가능한 슬롯 인덱스 반환
        // todo: 복수 슬롯을 반환할 수 있도록 변경 혹은 매서드 추가
        private bool TryGetValidSlot(IGameItem item, out int index)
        {
            if (item is not { GetItemInfo: EquipmentTypeSO equipmentData })
            {
                index = -1;
                return false;
            }

            index = (int)equipmentData.slotType;
            return true;
        }

        #endregion
        
        #region Store(Equip)
        
        // 아이템 장착 (빈 슬롯일 경우에만 사용 가능)
        public bool TryStore(IGameItem item)
        {
            if (TryGetValidSlot(item, out int index) && IsValidSlotIdx(index)) // 아이템을 장착할 수 있는 슬롯 인덱스 탐색 + 인덱스 유효성 검사
            {
                return TryStore(item, index);
            }

            return false;
        }

        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            if (TryGetValidSlot(item, out int index) && IsValidSlotIdx(index)) // 아이템을 장착할 수 있는 슬롯 인덱스 탐색 + 인덱스 유효성 검사
            {
                bool result = TryStore(item, index);
                storedSlot = result ? equipments[index] : null; // 아이템 저장 성공 시 저장 슬롯 반환
                return result;
            }

            storedSlot = null;
            return false;
        }

        public bool TryStore(IGameItem item, int index)
        {
            if (equipments[index] is {IsAccessible: true} slot) // 슬롯이 접근 가능하고 비어있는지 확인
            {
                bool result = (!slot.HasItem || TryRemoveItem(index)) && slot.TryStore(item); // 기존 아이템 제거 시도 및 
                if (result) NotifyEquip(item, index); // 아이템 장착 이벤트 호출
                
                return result; // 결과 반환
            }

            return false;
        }
        
        public bool TryStore(IGameItem item, out IGameItem existing)
        {
            if (TryGetValidSlot(item, out int index) && IsValidSlotIdx(index)) // 아이템을 장착할 수 있는 슬롯 인덱스 탐색 + 인덱스 유효성 검사
            {
                return TryStore(item, index, out existing);
            }

            existing = null;
            return false;
        }

        public bool TryStore(IGameItem item, int index, out IGameItem existing)
        {
            if (equipments[index] is not { IsAccessible: true } slot)
            {
                existing = null;
                return false;
            }

            existing = slot.HasItem ? slot.GetItem : null;
            return TryStore(item, index);
        }

        #endregion

        #region Remove(UnEquip)

        public bool TryRemoveItem(int index)
        {
            if (IsValidSlotIdx(index) && equipments[index] is 
                    { IsAccessible: true, HasItem: true} slot)
            {
                bool result = slot.Clear(out var item);
                if (result) NotifyUnEquip(item, index);
                return result;
            }

            return false;
        }

        public bool TryRemoveItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) && equipments[index] is 
                    { IsAccessible: true, HasItem: true} slot)
            {
                bool result = slot.Clear(out item);
                if (result) NotifyUnEquip(item, index);
                return result;
            }

            item = null;
            return false;
        }

        #endregion

        #region Validation

        private bool IsValidSlotIdx(int index)
        {
            //todo: 유효 인덱스 검사 로직 추가
            return index is >= 0 and < (int)Enums.EquippedItemSlotType.Max;
        }

        #endregion
        
        #region Notify Event

        private void NotifyEquip(IGameItem item, int slotIdx)
        {
            OnEquipmentChanged?.Invoke(this, new EquipArgs(item, EquipArgs.EquipEventState.Equip));
            NotifySlotChanged(slotIdx);
        }
        
        private void NotifyUnEquip(IGameItem item, int slotIdx)
        {
            OnEquipmentChanged?.Invoke(this, new EquipArgs(item, EquipArgs.EquipEventState.UnEquip));
            NotifySlotChanged(slotIdx);
        }

        private void NotifySlotChanged(int index)
        {
            OnSlotChanged?.Invoke(equipments[index]);
        }

        #endregion

        #region Save/Load (ISavable)

        
        public object CaptureState()
        {
            // //todo: 현재 장비 목록을 저장
            throw new NotImplementedException();
        }

        public bool RestoreState(object state)
        {
            // //todo: 저장된 장비 목록을 장착
            throw new NotImplementedException();
        }

        #endregion
        
        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is not CharacterTypeSO charInfo) return;

            foreach (var equipmentData in charInfo.defaultEquipments)
            {
                TryStore(new EquipmentItem(equipmentData));
            }
        }
    }
}


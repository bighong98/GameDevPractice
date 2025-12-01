using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Item.Storage
{
    public class PlayerQuickStorage : IGameItemStorage, IUsableItemStorage
    {
        public IReadOnlyCollection<IGameItemSlot> ItemSlots { get { 
            readonlySlots ??= slots.AsReadOnly(); 
            return readonlySlots; }}
        private readonly List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: QuickStorageCapacity);
        private IReadOnlyCollection<IGameItemSlot> readonlySlots;


        public int Capacity => QuickStorageCapacity;

        public event Action<IGameItemSlot> OnItemTryUsed;
        public event Action<IGameItemSlot> OnSlotChanged;
        public event Action OnStorageChanged;

        private const int QuickStorageCapacity = 5;

        public bool TryGetItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) && slots[index] is { IsAccessible: true, HasItem: true, GetItem: {} slotItem})
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

        public bool TryRemoveItem(int index)
        {
            if (!IsValidSlotIdx(index) || slots[index] == null) return false;
            slots[index] = null;
            return true;
        }

        public bool TryRemoveItem(int index, out IGameItem item)
        {
            if (IsValidSlotIdx(index) 
                && slots[index] is {IsAccessible: true, HasItem: true, GetItem: {} slotItem})
            {
                item = slotItem;
                slots[index] = null;
                return true;
            }

            item = default;
            return false;
        }

        public bool TryStore(IGameItem item)
        {
            throw new NotImplementedException();
        }

        public bool TryStore(IGameItem item, out IGameItemSlot storedSlot)
        {
            throw new NotImplementedException();
        }

        public bool TryStore(IGameItem item, int index)
        {
            throw new NotImplementedException();
        }

        public bool TryStoreAndUse(IGameItem item, object user = null)
        {
            throw new NotImplementedException();
        }

        public bool TryStoreAndUse(IGameItem item, int index, object user = null)
        {
            throw new NotImplementedException();
        }

        private bool IsValidSlotIdx(int index)
        {
            return index > 0 && index < Mathf.Min(slots.Count, Capacity);
        }
    }
}

// Assets/Scripts/Item/Storage/PlayerQuickStorage.cs 경로에 있는 PlayerQuickStorage 코드를 완성시켜줘

// 해당 클래스의 목적은 플레이어의 퀵슬롯(인벤토리의 아이템을 등록해두고  
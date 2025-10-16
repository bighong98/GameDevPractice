using System;
using TH.Item;
using UnityEngine;

namespace TH.UI
{
    public interface IPlayerInventoryUI
    {
        IPlayerStorageUI StorageUI { get; }
        IEquipmentHolderUI EquipmentUI { get; }
        event Action<DragSlotInfo> OnDrag;
        void MoveTooltip(Vector2 pos);
        void ShowTooltip(IGameItemSlot slot);
        void HideTooltip();
    }
}


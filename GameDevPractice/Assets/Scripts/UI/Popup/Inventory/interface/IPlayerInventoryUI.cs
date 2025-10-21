using System;
using TH.Item;
using UnityEngine;

namespace TH.UI
{
    public interface IPlayerInventoryUI : IFilterableStorageUI
    {
        IPlayerStorageUI StorageUI { get; }
        IEquipmentHolderUI EquipmentUI { get; }
        event Action<IDraggableStorageUI, int> OnDragStarted; // 드래그 시도 발생 (드래그 시작 시점에 트리거)
        event Action<DragSlotInfo> OnDragDrop; // 드래그&드랍 발생 (드랍 시점에 트리거)
        public void AllowDrag(Sprite sprite);
        public void CancelDrag();
        event Action OnExitUICalled; // 팝업 닫기 요청 발생
    }

    public interface IFilterableStorageUI
    {
        event Action<InventoryFilterType> OnFilterButtonPressed;
        void UpdateFilter(InventoryFilterType filter);
    }
}


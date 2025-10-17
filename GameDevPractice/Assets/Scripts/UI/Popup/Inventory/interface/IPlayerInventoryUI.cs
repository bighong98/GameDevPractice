using System;
using TH.Item;
using UnityEngine;

namespace TH.UI
{
    public interface IPlayerInventoryUI
    {
        IPlayerStorageUI StorageUI { get; }
        IEquipmentHolderUI EquipmentUI { get; }
        event Action<DragSlotInfo> OnDragDrop; // 드래그&드랍 발생 (드랍 시점에 트리거)
        event Action OnExitUICalled; // 팝업 닫기 요청 발생
        // 아이템 툴팁 관련 매서드
        // void MoveTooltip(Vector2 pos);
        // void ShowTooltip(IGameItemSlot slot);
        // void HideTooltip();
    }
}


using TH.Core;
using TH.Core.Service;
using TH.Resource;
using TH.UI;
using TH.Utils;

namespace TH.Item
{
    public sealed partial class InventoryController
    {
        // 슬롯 호버 처리(하이라이트/툴팁)
        private void OnSlotHovered(IHoverableStorageUI target, int index)
        {
            Logg.Log($"[{GetType().Name}] OnSlotHovered({target}, {index})", Logg.LoggingMode.Completed);

            // 드래그 중이면 해당 슬롯에 저장 가능한지 체크하여 경고 하이라이트 표시
            if (isDragging && dragSourceSlot != null)
            {
                if (GetStorageFromUI(target) is { } targetStorage &&
                    targetStorage.TryGetItemSlot(index, out var targetSlot))
                {
                    // 드래그 중인 아이템을 해당 슬롯에 저장할 수 있는지 확인
                    if (!targetSlot.CanStore(dragSourceSlot.GetItemInfo))
                    {
                        // 저장 불가능한 경우 경고 하이라이트 표시
                        if (target is IHighlightableStorageUI highlightStorageUI)
                        {
                            highlightStorageUI.HighlightSlot(index, (int)SlotHighlightType.Warn);
                        }
                        return; // 경고 하이라이트만 표시하고 일반 hover 처리는 하지 않음
                    }
                }
            }

            if (lastHovered.Source == target && index == lastHovered.Index) return; // 동일 슬롯은 무시
            if (lastHovered.IsValid())
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false); // 기존 하이라이트된 슬롯 하이라이트 비활성화
            SetHighlightSlot(source: target, index, true); // 새 슬롯 하이라이트 활성화
            // 현재 호버 슬롯 정보 갱신
            lastHovered.Source = target;
            lastHovered.Index = index;
            // 아이템이 있는 슬롯일 경우 아이템 툴팁 출력
            if (GetStorageFromUI(target) is { } result &&
                result.TryGetItemSlot(index, out var slot) &&
                slot is { HasItem: true, IsAccessible: true, GetItem: { } item })
            {
                UIManager.Instance.ShowUI<ItemTooltipUI>(ItemTooltipPrefabKey, UICanvas.FeedbackOverlay)
                    ?.ShowTooltipAt(InputManager.Instance.PointerPos, item);
            }
            else UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey); // 아이템이 없는 슬롯일 경우 툴팁 비활성화
        }

        // 슬롯 호버 해제 처리(하이라이트/툴팁 정리)
        private void OffSlotHovered(IHoverableStorageUI targetUI, int index)
        {
            // 기존에 다른 슬롯이 lastHovered에 기록되어있는 경우
            // 해당 슬롯도 하이라이트 비활성화
            Logg.Log($"[{GetType().Name}] OffSlotHovered({targetUI}, {index})", Logg.LoggingMode.Completed);

            // 드래그 중이고 경고 하이라이트가 표시된 경우 제거
            if (isDragging && targetUI is IHighlightableStorageUI highlightStorageUI)
            {
                highlightStorageUI.UnHighlightSlot(index, (int)SlotHighlightType.Warn);
            }

            if (lastHovered.IsValid() && !lastHovered.Equals(targetUI, index))
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false);
            // 타겟 슬롯(targetUI) 하이라이트 비활성화
            SetHighlightSlot(targetUI, index, false);
            // 호버링 슬롯 기록 초기화
            lastHovered.Clear();
            UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
        }

        // 슬롯 클릭 시 상세 툴팁 팝업 호출
        private void OnSlotClicked(IClickableStorageUI targetUI, int index)
        {
            if (GetStorageFromUI(targetUI) is not { } storage) return;
            if (!storage.TryGetItemSlot(index, out var slot)) return;

            ShowDetailedTooltip(storage, slot);
        }

        // 서브 클릭(사용) 입력 처리
        private void OnSlotSubClicked(ISubClickableStorageUI targetUI, int index)
        {
            if (GetStorageFromUI(targetUI) is not IUsableItemStorage storage) return;
            if (!storage.TryGetItemSlot(index, out var slot)) return;
            if (slot.GetItemInfo is not { isUsable: true }) return;

            HandleItemUse(storage, slot);
        }

        // 드래그 시작 처리(검증/하이라이트)
        private void OnDragStarted(IDraggableStorageUI sourceUI, int slotIndex)
        {
            if (GetStorageFromUI(sourceUI) is not { } storage) // UI로부터 스토리지를 찾을 수 없거나
            {
                Logg.LogError("[InventoryController] OnDragStarted() - failed to find storage from ui " + sourceUI);
                return;
            }

            if (!storage.TryGetItemSlot(slotIndex, out var slot) ||  // 슬롯을 찾을 수 없거나
                !slot.HasItem) // 해당 슬롯이 비어있다면
            {
                pInvenUI.CancelDrag(); // 드래그 취소
                ClearDragState(); // 드래그 상태 초기화
                return;
            }

            // 드래그 상태 저장
            isDragging = true;
            dragSourceStorage = storage;
            dragSourceSlot = slot;

            // Equipment 타입 아이템이면 저장 가능한 장비 슬롯 하이라이트
            if (slot.GetItemInfo is { itemType: Enums.ItemType.Equipment })
            {
                HighlightEquipmentSlots(slot.GetItemInfo);
            }

            pInvenUI.AllowDrag(slot.GetItemInfo.sprite); // 드래그 허가 및 UI에게 필요한 시각적 효과 출력 명령
        }

        // 드롭 처리(재배치 또는 전송)
        private void OnDragDrop(DragSlotInfo dragSlotInfo)
        {
            Logg.Log($"[InventoryController] DragDrop occured ({dragSlotInfo.From}, {dragSlotInfo.To})",
                Logg.LoggingMode.Completed);

            var from = dragSlotInfo.From;
            var fromSource = from.source;
            var to = dragSlotInfo.To;
            var toSource = to.source;

            if (GetStorageFromUI(fromSource) is not { } fromStorage
                || !fromStorage.TryGetItemSlot(from.index, out var fromSlot))
            {
                // 드래그 상태 초기화
                ClearDragState();
                return;
            }

            pInvenUI.CancelDrag();

            // 드래그 상태 초기화
            ClearDragState();

            // 기존 로직: toStorage 확인 후 동일 스토리지 내부 정렬 또는 일반 전송/교환
            if (GetStorageFromUI(toSource) is not { } toStorage
                || !toStorage.TryGetItemSlot(to.index, out var toSlot))
                return;

            if (fromStorage == toStorage && fromStorage is IRearrangeableStorage rStorage)
            {
                Logg.Log($"[InventoryController] trying to intra swap ({fromSlot}, {toSlot})",
                    Logg.LoggingMode.Completed);
                rStorage.TryTransferItem(fromSlot, toSlot);
            }
            else itemTransfer.TransferOrSwap(
                fromStorage, fromSlot, toStorage, toSlot);
        }

        // 드래그 상태 정리
        private void ClearDragState()
        {
            // 드래그 중이었던 아이템이 Equipment 타입이었다면 장비 슬롯 하이라이트 해제
            if (isDragging && dragSourceSlot != null &&
                dragSourceSlot.GetItemInfo is { itemType: Enums.ItemType.Equipment })
            {
                ClearEquipmentHighlights();
            }

            isDragging = false;
            dragSourceStorage = null;
            dragSourceSlot = null;
        }

        // 장착 가능한 슬롯만 하이라이트
        private void HighlightEquipmentSlots(ItemTypeSO draggedItemType)
        {
            if (pEquipHolder == null) return;
            if (pInvenUI.EquipmentUI is not IHighlightableStorageUI highlightUI) return;

            foreach (var slot in pEquipHolder.ItemSlots)
            {
                // 해당 슬롯에 드래그 중인 아이템을 저장할 수 있는지 확인
                if (slot.CanStore(draggedItemType))
                {
                    highlightUI.HighlightSlot(slot.Index);
                }
            }
        }

        // 장비 슬롯 하이라이트 해제
        private void ClearEquipmentHighlights()
        {
            if (pEquipHolder == null) return;
            if (pInvenUI.EquipmentUI is not IHighlightableStorageUI highlightUI) return;

            foreach (var slot in pEquipHolder.ItemSlots)
            {
                highlightUI.UnHighlightSlot(slot.Index);
            }
        }

        // 인벤토리 UI 닫기 요청 처리
        private void OnExitCalled()
        {
            UIManager.Instance.ClosePopupUI((PopupUI)pInvenUI);
        }

        // 필터 버튼 입력 처리
        private void OnFilterRequested(InventoryFilterType filter)
        {
            if (filter == currentFilter) return; // 현재 필터와 동일한 필터로 변경은 무시

            FilterStorage(pStorage, filter);
            currentFilter = filter;
            pInvenUI.UpdateFilter(filter);
        }

        // 빈 슬롯 정리 요청 처리
        private void OnInvenTrimRequested()
        {
            pStorage.Trim();
            RefreshStorageUI(pStorage);
        }

        // 정렬 요청 처리
        private void OnInvenSortRequested()
        {
            pStorage.Sort();
            RefreshStorageUI(pStorage);
        }
    }
}

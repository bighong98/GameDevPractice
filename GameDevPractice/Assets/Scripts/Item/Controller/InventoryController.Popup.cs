using System.Threading;
using TH.Core;
using TH.Core.Service;
using TH.UI;
using TH.Utils;

namespace TH.Item
{
    public sealed partial class InventoryController
    {
        // 상세 툴팁 팝업 구성 및 표시
        private void ShowDetailedTooltip(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item, GetItemInfo: { } itemInfo }) return;
            // 클로저 생성
            var targetStorage = storage;
            var targetSlot = slot;
            var slotCTS = AddNewItemModifyProgress(targetSlot);
            // 팝업 출력 시도
            if (UIManager.Instance.ShowPopupUI<ItemTooltipPopupUI>(ItemTooltipPopupKey) is not { } popup) return;
            // 팝업 CTS 체인 결합 및 내용 입력
            popup.ChainPopupCTS(slotCTS.Token);
            popup.SetTooltip(
                item: item,
                removeButton: new ButtonInfo(null,
                    itemInfo.itemType == Enums.ItemType.Special ? null : () => { ShowRemoveConfirmPopup(targetStorage, targetSlot); }),
                useButton: new ButtonInfo(GetUseButtonText(targetStorage, itemInfo),
                    itemInfo.isUsable && targetStorage is IUsableItemStorage usableStorage ? () =>
                    {
                        HandleItemUse(usableStorage, targetSlot); // 아이템 사용 효과 처리 (소비/장착/장착해제 등)
                        if (popup is { } validPopup) validPopup.ClosePopupUI(); // 이후 팝업 닫기
                    } : null // 사용할 수 없는 아이템이거나 아이템 사용이 불가능한 저장소인 경우 사용 버튼 비활성화
                ),
                divideButton: new ButtonInfo<int>(DefaultDivideText,
                    itemInfo.itemType == Enums.ItemType.Countable ? (expected) =>
                    {
                        HandleItemDivide(targetStorage, targetSlot, expected);
                        if (popup is { } validPopup) validPopup.ClosePopupUI(); // 이후 팝업 닫기
                    } : null // 개수 분리가 지원되지 않는 아이템의 경우 나누기 버튼 비활성화
                )
            );
        }

        // 삭제 확인 팝업 표시
        private void ShowRemoveConfirmPopup(IGameItemStorage storage, IGameItemSlot slot)
        {
            // 클로저 생성
            var targetStorage = storage;
            var targetSlot = slot;
            var slotCTS = AddNewItemModifyProgress(targetSlot);

            if (UIManager.Instance.ShowPopupUI<QuestionPopupUI>() is not { } popup) return;

            popup.ChainPopupCTS(slotCTS.Token);
            popup.SetQuestion(
                questionString: DefaultRemoveConfirmText,
                YesAction: () =>
                {
                    if (targetStorage == null || targetSlot == null) return; // 더이상 저장소와 슬롯 참조가 유효하지 않은 경우 취소
                    targetStorage.TryRemoveItem(targetSlot.Index); // todo: 아이템 제거에 실패한 경우 팝업을 닫는 대신 버리기 불가 안내
                    if (popup != null) popup.ClosePopupUI();
                },
                NoAction: () =>
                {
                    if (popup != null) popup.ClosePopupUI();
                });
        }

        // 슬롯 단위 작업 CTS 등록
        private CancellationTokenSource AddNewItemModifyProgress(IGameItemSlot slot)
        {
            Logg.Log($"{nameof(AddNewItemModifyProgress)}: {slot}", Logg.LoggingMode.Completed);
            if (progressingSlots.TryGetValue(slot, out var cts) &&
                !(cts?.IsCancellationRequested ?? true))
            {
                return cts;
            }

            var newCTS = new CancellationTokenSource();
            progressingSlots[slot] = newCTS;

            return newCTS;
        }

        // 변동이 발생한 슬롯과 연결된 작업 및 팝업 취소
        // 변경된 슬롯과 연결된 작업 취소
        private void CancelModifiedSlotProgress(IGameItemSlot slot)
        {
            if (slot == null) return;
            if (!progressingSlots.TryGetValue(slot, out var slotCTS)) return;

            ClearCTS(slotCTS); // Cancel and Dispose
            progressingSlots.Remove(slot);
        }

        // 모든 개별 슬롯과 연결된 작업 및 팝업 취소
        // 전체 슬롯 작업 취소
        private void CancelAllSlotProgress()
        {
            if (progressingSlots.Count == 0) return;
            foreach (var cts in progressingSlots.Values)
            {
                ClearCTS(cts);
            }
        }

        // CTS 안전 취소/해제
        private void ClearCTS(CancellationTokenSource tokenSource)
        {
            if (!tokenSource?.IsCancellationRequested ?? false)
                tokenSource.Cancel();
            tokenSource?.Dispose();
        }

        // 팝업/작업 상태 정리
        private void Refresh()
        {
            UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
            CancelAllSlotProgress();
        }
    }
}

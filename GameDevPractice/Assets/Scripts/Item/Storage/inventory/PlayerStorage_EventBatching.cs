namespace TH.Item
{
    // 슬롯/저장소 이벤트 배치 발행 partial
    public sealed partial class PlayerStorage
    {
        // 이벤트 배치 시작
        public void BeginEventBatch() => eventBatcher?.BeginEventBatch();
        // 이벤트 배치 종료
        public void EndEventBatch() => eventBatcher?.EndEventBatch();
        
        // 슬롯 이벤트 배칭 종료 후 슬롯 변동 사항 전달 
        void IStorageEventBatcher.NotifySlotChanged(int index) => eventBatcher?.NotifySlotChanged(index);
        // 저장소 전체 갱신 이벤트 전달 (현재는 EquipmentHolder 에서만 사용)
        void IStorageEventBatcher.NotifyStorageChanged() => eventBatcher?.NotifyStorageChanged();
        // 저장소 변경 이벤트 호출 메서드 내부 래퍼
        private void NotifyStorageChanged() => eventBatcher?.NotifyStorageChanged();

        // 인덱스 입력 기반 슬롯 변경 즉시 발행 경로
        private void NotifySlotChangedImmediate(int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (slots[index] is not { } slot) return;

            NotifySlotChangedImmediate(slot);
        }

        // 슬롯 입력 기반 필터 반영 후 변경 이벤트 즉시 발행
        private void NotifySlotChangedImmediate(IGameItemSlot slot)
        {
            slot.SetVisibility(IsVisibleByFilter(slot, CurrentFilter));
            OnSlotChanged?.Invoke(slot);
        }

        // 저장소 변경 이벤트 즉시 발행
        private void NotifyStorageChangedImmediate()
        {
            OnStorageChanged?.Invoke();
        }
    }
}

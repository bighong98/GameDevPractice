namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        public void BeginEventBatch() => eventBatcher?.BeginEventBatch();
        public void EndEventBatch() => eventBatcher?.EndEventBatch();
        void IStorageEventBatcher.NotifySlotChanged(int index) => eventBatcher?.NotifySlotChanged(index);
        void IStorageEventBatcher.NotifyStorageChanged() => eventBatcher?.NotifyStorageChanged();
        private void NotifyStorageChanged() => eventBatcher?.NotifyStorageChanged();

        private void NotifySlotChangedImmediate(int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (slots[index] is not { } slot) return;

            NotifySlotChangedImmediate(slot);
        }

        private void NotifySlotChangedImmediate(IGameItemSlot slot)
        {
            slot.SetVisibility(IsVisibleByFilter(slot, CurrentFilter));
            OnSlotChanged?.Invoke(slot);
        }

        private void NotifyStorageChangedImmediate()
        {
            OnStorageChanged?.Invoke();
        }
    }
}

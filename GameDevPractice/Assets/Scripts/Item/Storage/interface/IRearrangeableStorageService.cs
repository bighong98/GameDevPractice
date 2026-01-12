namespace TH.Item
{
    public interface IRearrangeableStorageService
    {
        bool TryTransferItem(IGameItemSlot fromSlot, IGameItemSlot toSlot);
        bool TryTransferItem(int fromIdx, int toIdx);
        void Trim();
        void Sort();
        void MergeStacks(bool trimAfter = false);
    }
}

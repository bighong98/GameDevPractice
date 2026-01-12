namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IRearrangeableStorage (Transfer/Swap/Trim/Sort)

        public bool TryTransferItem(IGameItemSlot fromSlot, IGameItemSlot toSlot)
            => rearrangeService.TryTransferItem(fromSlot, toSlot);

        public bool TryTransferItem(int fromIdx, int toIdx)
            => rearrangeService.TryTransferItem(fromIdx, toIdx);

        public void Trim()
            => rearrangeService.Trim();

        public void Sort()
            => rearrangeService.Sort();

        public void MergeStacks(bool trimAfter = false)
            => rearrangeService.MergeStacks(trimAfter);

        #endregion
    }
}

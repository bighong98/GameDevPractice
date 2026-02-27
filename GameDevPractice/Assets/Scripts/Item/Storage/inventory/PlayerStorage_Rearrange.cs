namespace TH.Item
{
    // 슬롯 재배치/정렬 규칙 위임 partial
    public sealed partial class PlayerStorage
    {
        #region IRearrangeableStorage (Transfer/Swap/Trim/Sort)

        // 슬롯 참조 기반 전송/교환 요청 위임
        public bool TryTransferItem(IGameItemSlot fromSlot, IGameItemSlot toSlot)
            => rearrangeService.TryTransferItem(fromSlot, toSlot);

        // 슬롯 인덱스 기반 전송/교환 요청 위임
        public bool TryTransferItem(int fromIdx, int toIdx)
            => rearrangeService.TryTransferItem(fromIdx, toIdx);

        // 빈 슬롯 압축 요청 위임
        public void Trim()
            => rearrangeService.Trim();

        // 정렬 요청 위임
        public void Sort()
            => rearrangeService.Sort();

        // 스택 병합 요청 위임
        public void MergeStacks(bool trimAfter = false)
            => rearrangeService.MergeStacks(trimAfter);

        #endregion
    }
}

namespace TH.Item
{
    // 아이템 교체/꺼내기 규칙 위임 partial
    public sealed partial class PlayerStorage
    {
        #region IReplaceableStorage

        // 빈 슬롯 우선 교체 저장 요청 위임
        public bool TryReplace(IGameItem item, out IGameItem existing)
            => replaceService.TryReplace(item, out existing);

        // 교체 결과 슬롯 반환 포함 요청 위임
        public bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing)
            => replaceService.TryReplace(item, out storedSlot, out existing);

        // 지정 인덱스 교체 요청 위임
        public bool TryReplaceAt(IGameItem item, int index, out IGameItem existing)
            => replaceService.TryReplaceAt(item, index, out existing);

        // 지정 슬롯 아이템 꺼내기 요청 위임
        public bool TryTakeOut(int index, out IGameItem item)
            => replaceService.TryTakeOut(index, out item);

        #endregion
    }
}

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IReplaceableStorage

        public bool TryReplace(IGameItem item, out IGameItem existing)
            => replaceService.TryReplace(item, out existing);

        public bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing)
            => replaceService.TryReplace(item, out storedSlot, out existing);

        public bool TryReplaceAt(IGameItem item, int index, out IGameItem existing)
            => replaceService.TryReplaceAt(item, index, out existing);

        public bool TryTakeOut(int index, out IGameItem item)
            => replaceService.TryTakeOut(index, out item);

        #endregion
    }
}

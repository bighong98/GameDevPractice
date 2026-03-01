namespace TH.Item
{
    public interface IReplaceableStorageService
    {
        bool TryReplace(IGameItem item, out IGameItem existing);
        bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing);
        bool TryReplaceAt(IGameItem item, int index, out IGameItem existing);
        bool TryTakeOut(int index, out IGameItem item);
    }
}

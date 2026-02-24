using TH.Resource;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Item index cahce

        private void CacheAdd(ItemTypeSO itemInfo, int index)
        {
            itemIndexCache.Add(itemInfo, index);
        }

        private void CacheAdd(IGameItem item, int index)
        {
            itemIndexCache.Add(item, index);
        }

        private void CacheRemove(IGameItem item, int index)
        {
            itemIndexCache.Remove(item, index);
        }

        // Remove cache entry when a slot is cleared
        private void CacheClearSlot(IGameItemSlot slot)
        {
            itemIndexCache.ClearSlot(slot);
        }

        // Try cache first
        private bool TryGetCachedIndex(ItemTypeSO data, int minIndex, out int index)
        {
            return itemIndexCache.TryGetCachedIndex(data, minIndex, out index);
        }

        private void RebuildItemIndexCache()
        {
            itemIndexCache.Rebuild();
        }

        #endregion
    }
}

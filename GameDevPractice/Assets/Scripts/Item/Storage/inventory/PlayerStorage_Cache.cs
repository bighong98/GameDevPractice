using TH.Resource;

namespace TH.Item
{
    // 플레이어 인벤토리 캐시 접근/갱신 partial
    public sealed partial class PlayerStorage
    {
        #region Item index cahce

        // 타입 기반 인덱스 캐시 추가 래퍼
        private void CacheAdd(ItemTypeSO itemInfo, int index)
        {
            itemIndexCache.Add(itemInfo, index);
        }

        // 아이템 인스턴스 기반 인덱스 캐시 추가 래퍼
        private void CacheAdd(IGameItem item, int index)
        {
            itemIndexCache.Add(item, index);
        }

        // 아이템 인덱스 캐시 제거 래퍼
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
        // 캐시 우선 조회 래퍼
        private bool TryGetCachedIndex(ItemTypeSO data, int minIndex, out int index)
        {
            return itemIndexCache.TryGetCachedIndex(data, minIndex, out index);
        }

        // 슬롯 전체 스캔 기반 캐시 재구축 래퍼
        private void RebuildItemIndexCache()
        {
            itemIndexCache.Rebuild();
        }

        #endregion
    }
}

using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    // 저장 후 사용/직접 사용 규칙 partial
    public sealed partial class PlayerStorage
    {
        #region IUsableItemStorage
        
        // 저장 성공 직후 사용 이벤트 발행 경로
        public bool TryStoreAndUse(IGameItem item, object user = null)
        {
            if (!TryStore(item, out var storedSlot)) return false;
            
            this.Log($"TryStoreAndUse({item}) - Store succeed. call OnItemTryUsed.Invoke({storedSlot})", Logg.LoggingMode.Completed);
            OnItemTryUsed?.Invoke(storedSlot);
            return true;
        }

        #endregion

        #region IStoreAndUse

        // 지정 슬롯 저장 성공 직후 사용 이벤트 발행 경로
        public bool TryStoreAndUse(IGameItem item, int index, object user = null)
        {
            if (!TryStore(item, index)) return false;
            
            OnItemTryUsed?.Invoke(slots[index]);
            return true;
        }

        // 타입 기반 대상 슬롯 탐색 후 사용 이벤트 발행
        public bool TryUse(ItemTypeSO itemInfo, object user = null, int amount = 1)
        {
            if (itemInfo == null || !itemInfo.IsNotNull())
                return false;

            if (!TryFindSlot(itemInfo, out var slot))
                return false;

            OnItemTryUsed?.Invoke(slot);
            return true;
        }
        
        #endregion
    }
}

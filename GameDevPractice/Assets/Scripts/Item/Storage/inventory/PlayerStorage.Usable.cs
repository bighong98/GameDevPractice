using TH.Utils;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region IUsableItemStorage
        
        public bool TryStoreAndUse(IGameItem item, object user = null)
        {
            if (!TryStore(item, out var storedSlot)) return false;
            
            this.Log($"TryStoreAndUse({item}) - Store succeed. call OnItemTryUsed.Invoke({storedSlot})", Logg.LoggingMode.Completed);
            OnItemTryUsed?.Invoke(storedSlot);
            return true;
        }

        public bool TryStoreAndUse(IGameItem item, int index, object user = null)
        {
            if (!TryStore(item, index)) return false;
            
            OnItemTryUsed?.Invoke(slots[index]);
            return true;
        }
        
        #endregion
    }
}


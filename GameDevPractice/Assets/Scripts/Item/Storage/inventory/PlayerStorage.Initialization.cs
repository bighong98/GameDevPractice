using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Initialization

        private void Init()
        {
            SetCapacity(InitialCapacity);
            Clear();
        }

        private const string InventoryTestDataSOKey = "InventoryTestDataSO";
        private bool isTestDataLoaded = false;
        private bool _hasRestoredState = false;

        
        private void LoadTestData(IResourceLoader resourceLoader)
        {
            if (isTestDataLoaded || _hasRestoredState) return;
            
            if (!resourceLoader.TryLoad<InventoryTestDataSO>(InventoryTestDataSOKey, out var testData))
            {
                Logg.LogError("TestData is null");
                return;
            }

            foreach (var (itemReference, amount) in testData.Items)
            {
                if (!resourceLoader.TryLoad<ItemTypeSO>(itemReference, out var item))
                {
                    Logg.LogError($"[{GetType().Name} - LoadTestData] Trying to load item from ({itemReference}, {amount})");
                    continue;
                }
                Logg.Log($"Trying to add ({item.nameString}, {amount})", Logg.LoggingMode.Completed);
                if (!TryStore(EnsureItemInstanceByType(item, amount)))
                {
                    Logg.LogError($"[PlayerInventory] failed to add test data item ({item.nameString}, {amount})");
                }
            }

            isTestDataLoaded = true;
        }
        
        #endregion
    }
}

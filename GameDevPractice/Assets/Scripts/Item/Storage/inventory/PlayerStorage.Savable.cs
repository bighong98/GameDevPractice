using System.Collections.Generic;
using TH.Utils;
using UnityEngine.SceneManagement;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region ISavable (save/load)

        private const string InventoryIdentifier = "playerInventory";
        public string UniqueIdentifier => InventoryIdentifier;
        public bool IsGlobal { get; } = true;
        public bool IsRegistered {get; set;} = false;
        public Scene TargetScene { get; } = default;

        public object CaptureState()
        {
            this.Log($"CaptureState", Logg.LoggingMode.Completed);
            List<IGameItem> items = new();

            foreach (var slot in slots)
            {
                if (slot is not { HasItem: true, GetItem: { } item }) continue;
                items.Add(item.Clone<IGameItem>());
            }

            return items;
        }

        public bool RestoreState(object state)
        {
            this.Log($"RestoreState", Logg.LoggingMode.Completed);

            Clear();

            List<IGameItem> items = ExtractSaveData(state);
            _hasRestoredState = items is { Count: > 0 };

            if (items is { Count: > 0 })
            {
                foreach (var item in items)
                {
                    TryStore(itemBuilder.GetItemFromData(item.GetItemInfo, item.GetAmount));
                }
            }

            NotifyStorageChanged();

            return true;
        }

        private static List<IGameItem> ExtractSaveData(object state)
        {
            switch (state)
            {
                case List<IGameItem> l: return l;
                case Dictionary<string, object> stateDict:
                {
                    foreach (var s in stateDict.Values)
                        if (s is List<IGameItem> { } dl)
                            return dl;
                    break;
                }
            }
            return null;
        }

        #endregion
    }
}

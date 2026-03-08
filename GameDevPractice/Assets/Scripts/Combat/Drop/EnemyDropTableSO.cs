using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat.Drop
{
    [Serializable]
    public sealed class EnemyDropEntry
    {
        [SerializeField] private AssetReferenceItemSO itemReference;
        [Range(0f, 1f)] public float chance = 1f;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;

        [NonSerialized] private ItemTypeSO cachedItem;
        [NonSerialized] private string cachedGuid;
        [NonSerialized] private bool initialized;

        public async UniTask InitializeAsync(CancellationToken token)
        {
            if (initialized)
                return;

            if (itemReference != null && itemReference.RuntimeKeyIsValid() && ResourceManager.Instance != null)
            {
                var loadedItem = await ResourceManager.Instance.ExtractAssetRefAsync<ItemTypeSO>(itemReference, token);
                if (loadedItem != null)
                {
                    cachedItem = loadedItem;
                    cachedGuid = itemReference.AssetGUID;
                }
            }

            initialized = true;
        }

        public bool TryGetItem(out ItemTypeSO item)
        {
            item = null;
            if (itemReference == null || !itemReference.RuntimeKeyIsValid())
                return false;

            var guid = itemReference.AssetGUID;
            if (cachedItem != null && string.Equals(cachedGuid, guid, StringComparison.OrdinalIgnoreCase))
            {
                item = cachedItem;
                return true;
            }

            if (ResourceManager.Instance == null)
                return false;

            if (!ResourceManager.Instance.TryLoad(itemReference, out ItemTypeSO loaded) || loaded == null)
                return false;

            cachedItem = loaded;
            cachedGuid = guid;
            item = loaded;
            return true;
        }
    }

    [CreateAssetMenu(fileName = "EnemyDropTableSO", menuName = "Scriptable Objects/Drop/EnemyDropTableSO")]
    public sealed class EnemyDropTableSO : ScriptableObject, IAsyncInitializer
    {
        [SerializeField] private List<EnemyDropEntry> entries = new();
        [NonSerialized] private bool initialized;

        public IReadOnlyList<EnemyDropEntry> Entries => entries;
        public bool HasEntries => entries != null && entries.Count > 0;

        public async UniTask InitializeAsync(CancellationToken token)
        {
            if (initialized)
                return;

            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null)
                        continue;

                    await entry.InitializeAsync(token);
                }
            }

            initialized = true;
        }
    }
}

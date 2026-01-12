using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using TH.Resource;
using TH.Item.Storage;
using System.Linq;
using UnityEngine.SceneManagement;


namespace TH.Item
{
    public sealed partial class PlayerStorage : IPlayerStorage, ISavableEntity
    {
        public event Action<IGameItemSlot> OnSlotChanged; // Use NotifySlotChanged instead of direct invoke
        public event Action OnStorageChanged;
        public event Action<IGameItemSlot> OnItemTryUsed; 
        public event Action<int> OnCapacityChanged;
        public event Action<InventoryFilterType> OnFilterChanged;

        public InventoryFilterType CurrentFilter { get; private set; } = InventoryFilterType.All;

        public IReadOnlyCollection<IGameItemSlot> ItemSlots => slots;
        private List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: maxCapacity);
        
        // internal caches
        private readonly CountableAmountCache countableCache;
        private readonly ItemIndexCache itemIndexCache;
        
        // internal services (composition)
        private readonly ICountableStorageService countableService;
        private readonly IRearrangeableStorageService rearrangeService;
        private readonly IConsumableStorageService consumableService;
        private readonly IReplaceableStorageService replaceService;

        public int Capacity => capacity;
        private int capacity;
        public int MaxCapacity => maxCapacity;
        private const int maxCapacity = 256;
        private const int InitialCapacity = 80;
        
        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        public PlayerStorage(IResourceLoader resourceLoader, ISaveEntityRegistry saveEntityRegistry)
        {
            countableCache = new CountableAmountCache(RaiseCountableAmountModified);
            itemIndexCache = new ItemIndexCache(IsValidSlotIdx, GetSlot, () => GetEndIdx);
            
            countableService = new CountableStorageService(
                IsValidSlotIdx,
                () => GetEndIdx,
                GetSlot,
                FindEmptySlot,
                TryStoreInternal,
                NotifySlotChanged,
                NotifySlotChanged,
                countableCache,
                itemIndexCache);

            rearrangeService = new RearrangeStorageService(
                IsValidSlotIdx,
                GetSlot,
                () => GetEndIdx,
                () => capacity,
                FindEmptySlot,
                TryRemoveItem,
                CacheAdd,
                CacheRemove,
                NotifySlotChanged,
                RebuildItemIndexCache,
                countableService);

            consumableService = new ConsumableStorageService(
                IsValidSlotIdx,
                GetSlot,
                () => GetEndIdx,
                TryGetCountableAmount,
                TryGetCachedIndex,
                UpdateCountableDict,
                NotifySlotChanged,
                CacheAdd,
                TryRemoveItem);

            replaceService = new ReplaceStorageService(
                IsValidSlotIdx,
                GetSlot,
                FindEmptySlot,
                TryGetItemSlot,
                TryStore,
                UpdateCountableDict,
                CacheRemove,
                NotifySlotChanged);

            Init();

            resourceLoader.OnLabelResourcesLoadedAll += (label) =>
            {
                if (!string.Equals(label, Constants.PreLoadLabel)) return;
                saveEntityRegistry.RegisterEntity(this);
                LoadTestData(resourceLoader);
            };
        }


        private void Clear()
        {
            slots.Clear();
            FillInventoryWithEmptySlots();

            countableCache.Clear();
            itemIndexCache.Clear();
        }
    }
}


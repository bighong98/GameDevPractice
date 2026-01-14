using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using TH.Resource;
using TH.Item.Storage;

namespace TH.Item
{
    public sealed partial class PlayerStorage : IPlayerStorage, ISavableEntity, IStorageEventBatcher
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
        
        // internal modules (composition)
        private readonly ICountableStorageService countableService;
        private readonly IRearrangeableStorageService rearrangeService;
        private readonly IConsumableStorageService consumableService;
        private readonly IReplaceableStorageService replaceService;
        private readonly IStorageEventBatcher eventBatcher;

        public int Capacity => capacity;
        private int capacity;
        public int MaxCapacity => maxCapacity;
        private const int maxCapacity = 256;
        private const int InitialCapacity = 80;
        
        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        public PlayerStorage(IResourceLoader resourceLoader, ISaveEntityRegistry saveEntityRegistry)
        {
            // 내부 캐시 생성
            countableCache = new CountableAmountCache(RaiseCountableAmountModified);
            itemIndexCache = new ItemIndexCache(IsValidSlotIdx, GetSlot, () => GetEndIdx);
            
            // 내부 서비스 모듈 생성 (+ 생성자 의존성 주입)
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

            eventBatcher = new StorageEventBatcher(NotifySlotChangedImmediate, NotifyStorageChangedImmediate);

            Init();

            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, () =>
            {
                saveEntityRegistry.RegisterEntity(this);
                LoadTestData(resourceLoader);
            });
        }

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

        private void Clear()
        {
            slots.Clear();
            FillInventoryWithEmptySlots();

            countableCache.Clear();
            itemIndexCache.Clear();
        }
    }
}


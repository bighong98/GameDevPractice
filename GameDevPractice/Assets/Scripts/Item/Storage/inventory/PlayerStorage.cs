using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using TH.Resource;
using TH.Item.Storage;

namespace TH.Item
{
    // 플레이어 인벤토리 저장소 본체 및 모듈 조합 루트 partial
    public sealed partial class PlayerStorage : IPlayerStorage, ISavableEntity, IStorageEventBatcher
    {
        // 슬롯 단위 변경 알림 이벤트
        public event Action<IGameItemSlot> OnSlotChanged; // Use NotifySlotChanged instead of direct invoke
        // 저장소 전체 변경 알림 이벤트
        public event Action OnStorageChanged;
        // 아이템 사용 시도 알림 이벤트
        public event Action<IGameItemSlot> OnItemTryUsed; 
        // 용량 변경 알림 이벤트
        public event Action<int> OnCapacityChanged;
        // 필터 변경 알림 이벤트
        public event Action<InventoryFilterType> OnFilterChanged;

        // 현재 인벤토리 필터 상태
        public InventoryFilterType CurrentFilter { get; private set; } = InventoryFilterType.All;

        // 외부 노출 전용 슬롯 컬렉션
        public IReadOnlyCollection<IGameItemSlot> ItemSlots => slots;
        // 내부 슬롯 리스트 저장소
        private List<IGameItemSlot> slots = new List<IGameItemSlot>(capacity: maxCapacity);
        
        // internal caches
        // 수량형 아이템 합계 캐시
        private readonly CountableAmountCache countableCache;
        // 아이템 타입별 슬롯 인덱스 캐시
        private readonly ItemIndexCache itemIndexCache;
        
        // internal modules (composition)
        // 수량형 저장 규칙 모듈
        private readonly ICountableStorageService countableService;
        // 재배치/정렬/병합 모듈
        private readonly IRearrangeableStorageService rearrangeService;
        // 소비 규칙 모듈
        private readonly IConsumableStorageService consumableService;
        // 교체/꺼내기 규칙 모듈
        private readonly IReplaceableStorageService replaceService;
        // 이벤트 배치 발행 모듈
        private readonly IStorageEventBatcher eventBatcher;

        // 현재 활성 용량 값
        public int Capacity => capacity;
        private int capacity;
        // 용량 최대치 상수
        public int MaxCapacity => maxCapacity;
        private const int maxCapacity = 256;
        private const int InitialCapacity = 80;
        
        // 유효 슬롯 마지막 인덱스 계산 프로퍼티
        private int GetEndIdx => Mathf.Min(capacity, slots.Count) - 1; // return value -1 means not initialized or cleared 
        // 슬롯 인덱스 유효 범위 검증 유틸리티
        private bool IsValidSlotIdx(int index) => index >= 0 && index <= GetEndIdx;
        
        // 의존 모듈 구성 및 초기 데이터 로드 연계 생성자
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

        // 초기 용량 설정 및 빈 슬롯 구성
        private void Init()
        {
            SetCapacity(InitialCapacity);
            Clear();
        }

        private const string InventoryTestDataSOKey = "InventoryTestDataSO";
        private bool isTestDataLoaded = false;
        private bool _hasRestoredState = false;

        
        // 테스트 데이터 로드 및 저장소 시드 주입
        private void LoadTestData(IResourceLoader resourceLoader)
        {
            // 저장 복원 우선 보장을 위한 테스트 데이터 로드 차단 가드
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

        // 슬롯/캐시 전체 초기화
        private void Clear()
        {
            slots.Clear();
            FillInventoryWithEmptySlots();

            countableCache.Clear();
            itemIndexCache.Clear();
        }
    }
}


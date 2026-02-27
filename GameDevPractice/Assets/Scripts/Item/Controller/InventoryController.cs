using System;
using System.Collections.Generic;
using System.Threading;
using TH.UI;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using TH.Item.Storage;
using TH.Attribute;
using TH.UI.Data;
using Cysharp.Threading.Tasks;
using TH.Core;

namespace TH.Item
{
    // 인벤토리 UI 입력/이벤트/팝업 상호작용(View) <-> 인벤토리 데이터 스토리지 (Model)
    // View와 Model 간 상호작용 중계 역할
    public sealed partial class InventoryController: MonoBehaviour
    {
        // model
        private IPlayerStorage pStorage;
        private IQuickStorage pQuickStorage;
        private IEquipmentHolder pEquipHolder;
        // view
        private IPlayerInventoryUI pInvenUI;

        #region const strings
        private const string ItemTooltipPrefabKey = "UI_ItemTooltip.prefab";
        private const string ItemTooltipPopupKey = "ItemTooltipPopupUI";
        private const string DefaultRemoveText = "버리기";
        private const string DefaultConsumeText = "사용";
        private const string DefaultEquipText = "장착";
        private const string DefaultUnEquipText = "장착해제";
        private const string DefaultDivideText = "나누기";
        private const string DefaultRemoveConfirmText = "아이템을 정말 파괴하시겠습니까?";
        #endregion

        // 드래그 시작 원본 스토리지/슬롯 및 마지막 호버 슬롯 추적
        private bool isDragging = false;
        private IGameItemStorage dragSourceStorage = null;
        private IGameItemSlot dragSourceSlot = null;
        private SlotUIInfo<IHoverableStorageUI> lastHovered;
        private InventoryFilterType currentFilter = InventoryFilterType.All;

        // outer service
        private IGameItemTransfer itemTransfer;
        private IGameItemConsumer itemConsumer;

        private Health playerHealth;

        // Input Events (UI - View)
        // 슬롯 호버/클릭/서브클릭 이벤트 핸들러 등록 관리
        private EventHandlerRegistry<IHoverableStorageUI, int> _hoverEnterRegistry;
        private EventHandlerRegistry<IHoverableStorageUI, int> _hoverExitRegistry;
        private EventHandlerRegistry<IClickableStorageUI, int> _clickRegistry;
        private EventHandlerRegistry<ISubClickableStorageUI, int> _subClickRegistry;

        // Storage Events (Model)
        // 슬롯 변경/용량 변경/사용 시도 이벤트 핸들러 등록 관리
        private EventHandlerRegistry<IGameItemStorage, IGameItemSlot> _slotChangedRegistry = null;
        private EventHandlerRegistry<IUsableItemStorage, IGameItemSlot> _tryUsedRegistry = null;
        private EventHandlerRegistry<IMutableCapacity, int> _capacityRegistry = null;

        private readonly Dictionary<IGameItemStorage, Action> _storageChangedHandlers = new();

        // 현재 유저가 상호작용 중인 (상세 팝업 호출, 아이템 버리기 팝업 호출 등) 작업 목록 <슬롯, CTS> 
        private readonly Dictionary<IGameItemSlot, CancellationTokenSource> progressingSlots = new();

        // 의존성 주입 및 초기 상태 세팅
        private void Awake()
        {
            // EventHandlerRegistry 초기화
            InitializeEventRegistries();
            // 외부 서비스 참조 받아오기
            pStorage = ServiceLocator.Get<IPlayerStorage>();
            pQuickStorage = ServiceLocator.Get<IQuickStorage>();
            itemTransfer = ServiceLocator.Get<IGameItemTransfer>();
            itemConsumer = ServiceLocator.Get<IGameItemConsumer>();
            // InventoryUI 참조 받기 -> 없는 경우 InventoryController 비활성화 및 중단
            if (!TryGetComponent(out pInvenUI))
            {
                Logg.LogError($"[{nameof(InventoryController)}] " +
                              $"failed to GetComponent<{nameof(IPlayerInventoryUI)}>. disable inventory controller");
                this.enabled = false;
                return;
            }

            // 스토리지(Model) 이벤트 바인드
            BindStorageEvents(pStorage); // 드랍 장비 아이템 자동 장착 버그 대응(Start -> Awake)
            RenewPlayerReference();
            
            UpdatePlayerStatusPanel();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            // 씬 재진입 시 플레이어 참조 재동기화
            RenewPlayerReference(); // 플레이어의 EquipHolder 인스턴스 참조 및 이벤트 갱신
            UpdatePlayerStatusPanel();
        }

        // 플레이어 상태 패널 대상 오브젝트 동기화
        private void UpdatePlayerStatusPanel()
        {
            if (pInvenUI is not InventoryUI inventoryUI) return;
            if (playerHealth == null) return;

            inventoryUI.SetPlayerStatusPanel(playerHealth.gameObject);
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // 씬 로드 완료 시 참조/패널 동기화
            RenewPlayerReference();
            UpdatePlayerStatusPanel();
        }


        // 이벤트 바인딩 후 UI 초기 갱신
        private void Start()
        {
            if (pInvenUI is not { StorageUI: { } storageUI, EquipmentUI: { } equipmentUI })
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to get {nameof(IPlayerStorageUI)}, " +
                              $"{nameof(IEquipmentHolderUI)} from {nameof(IPlayerInventoryUI)}");
                return;
            }

            // UI(View) 이벤트 바인드
            BindStorageUIEvents(pStorage);
            BindStorageUIEvents(pEquipHolder);
            BindButtonEvents();
            BindDragDropUIEvents();

            // 일회성 강제 갱신
            OnStorageCapacityChanged(pStorage, pStorage.Capacity);
            pInvenUI.UpdateFilter(currentFilter);
            RefreshStorageUI();
            RefreshEquipmentUI();

            if (ResourceManager.Instance.TryLoad<InventorySFXCatalogSO>("InventorySFXCatalogSO", out var catalog))
            {
                pInvenUI.SetSfx(LoadInventorySFX(catalog));
            }
        }

        private void OnDisable()
        {
            // 비활성 전환 시 팝업/작업 상태 정리
            Refresh();
        }

        // 이벤트 해제 및 임시 상태 정리
        private void OnDestroy()
        {
            Refresh();

            // UnBindStorageEvents(pStorage);
            // UnBindStorageEvents(pEquipHolder);
            ClearStorageModifiedEvent();
            _storageChangedHandlers.Clear();
            _slotChangedRegistry.Clear();
            _capacityRegistry.Clear();
            _tryUsedRegistry.Clear();

            _hoverEnterRegistry.Clear();
            _hoverExitRegistry.Clear();
            _clickRegistry.Clear();
            _subClickRegistry.Clear();

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}


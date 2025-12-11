using System;
using System.Collections.Generic;
using System.Threading;
using TH.Control;
using TH.UI;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using TH.Item.Storage;
using TH.Attribute;
using TH.UI.Data;
using Cysharp.Threading.Tasks;

namespace TH.Item
{
    public sealed class InventoryController: MonoBehaviour
    {
        // model
        private IPlayerStorage pStorage;
        private IQuickStorage pQuickStorage;
        private IEquipmentHolder pEquipHolder;
        // view
        private IPlayerInventoryUI pInvenUI;
        // sub popup
        private UI_ItemTooltip itemTooltip;

        
        
        // 드래그 상태 추적
        private bool isDragging = false;
        private IGameItemStorage dragSourceStorage = null;
        private IGameItemSlot dragSourceSlot = null;
        private SlotUIInfo<IHoverableStorageUI> lastHovered;
        private InventoryFilterType currentFilter = InventoryFilterType.All;
        
        // outer service
        private IGameItemTransfer itemTransfer;
        private IGameItemConsumer itemConsumer;

        
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

            RenewPlayerReference();
            SceneManager.sceneLoaded += RenewPlayerReference;
            
            LoadItemTooltipUI();
        }

        private void OnEnable()
        {
            RenewPlayerReference(); // 플레이어의 EquipHolder 인스턴스 참조 및 이벤트 갱신
        }
        
        private void Start()
        {
            if (pInvenUI is not { StorageUI: { } storageUI, EquipmentUI: { } equipmentUI })
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to get {nameof(IPlayerStorageUI)}, " +
                              $"{nameof(IEquipmentHolderUI)} from {nameof(IPlayerInventoryUI)}");
                return;
            }

            // 스토리지(Model) 이벤트 바인드
            BindStorageEvents(pStorage);
            BindStorageEvents(pEquipHolder);
            
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
            Refresh();
        }

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

            if (itemTooltip != null && itemTooltip.gameObject != null)
                Destroy(itemTooltip.gameObject);

            SceneManager.sceneLoaded -= RenewPlayerReference;
        }


        #region Initialization

        private static async UniTask<object> LoadInventorySFX(InventorySFXCatalogSO catalog)
        {
            Dictionary<InventorySFX, AudioClip> dict = new();
            foreach (var (key, value) in catalog.Items)
            {
                dict[key] = await ResourceManager.Instance.ExtractAssetRefAsync(value);
            }

            return dict;
        }

        // EventHandlerRegistry<> 인스턴스 초기화
        private void InitializeEventRegistries()
        {
            _hoverEnterRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotHovered += handler,
                remover: (ui, handler) => ui.OnSlotHovered -= handler
            );
            _hoverExitRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
                adder: (ui, handler) => ui.OffSlotHovered += handler,
                remover: (ui, handler) => ui.OffSlotHovered -= handler
            );
            _clickRegistry = new EventHandlerRegistry<IClickableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotClicked += handler,
                remover: (ui, handler) => ui.OnSlotClicked -= handler
            );
            _subClickRegistry = new EventHandlerRegistry<ISubClickableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotSubClicked += handler,
                remover: (ui, handler) => ui.OnSlotSubClicked -= handler
            );
            _slotChangedRegistry = new EventHandlerRegistry<IGameItemStorage, IGameItemSlot>(
                adder: (storage, handler) => storage.OnSlotChanged += handler,
                remover: (storage, handler) => storage.OnSlotChanged -= handler
            );
            _tryUsedRegistry = new EventHandlerRegistry<IUsableItemStorage, IGameItemSlot>(
                adder: (storage, handler) => storage.OnItemTryUsed += handler,
                remover: (storage, handler) => storage.OnItemTryUsed -= handler
            );
            _capacityRegistry = new EventHandlerRegistry<IMutableCapacity, int>(
                adder: (storage, handler) => storage.OnCapacityChanged += handler,
                remover: (storage, handler) => storage.OnCapacityChanged -= handler
            );
        }

        // View(UI) Event Bind
        private void BindStorageUIEvents(IGameItemStorage storage)
        {
            if (GetUIFromStorage(storage) is not { } storageUI) return;

            if (storageUI is IHoverableStorageUI hStorageUI)
            {
                SubscribeHoverEnterEvent(hStorageUI);
                SubscribeHoverExitEvent(hStorageUI);
            }
            if (storageUI is IClickableStorageUI cStorageUI)
                SubscribeClickEvent(cStorageUI);
            if (storageUI is ISubClickableStorageUI scStorageUI)
                SubscribeSubClickEvent(scStorageUI);
        }

        private void BindButtonEvents()
        {
            pInvenUI.OnExitUICalled += OnExitCalled;
            pInvenUI.OnFilterButtonPressed += OnFilterRequested;
            pInvenUI.OnSortButtonPressed += OnInvenSortRequested;
            pInvenUI.OnTrimButtonPressed += OnInvenTrimRequested;
        }
        
        private void BindDragDropUIEvents()
        {
            pInvenUI.OnDragStarted += this.OnDragStarted;
            pInvenUI.OnDragDrop += this.OnDragDrop;
        }

        // Model(Storage) Event Bind
        private void BindStorageEvents(IGameItemStorage storage)
        {
            if (storage == null) return;
         
            // 개별 Subscribe 계열 매서드들이 반드시 UnSubscribe 이후 구독하도록 할 것 (이벤트 핸들러 누적 방지)
            // EventHandlerRegistry<> 로 관리되는 경우 Register() 호출 시 내부에서 UnRegister() 자동 호출됨
            SubscribeStorageModifiedEvent(storage);
            SubscribeSlotModifiedEvent(storage);
            if (storage is IMutableCapacity cStorage)
                SubscribeStorageCapacityEvent(cStorage);
            if (storage is IUsableItemStorage uStorage)
                SubscribeStorageUsageEvent(uStorage);
        }
        
        private void UnBindStorageEvents(IGameItemStorage storage)
        {
            if (storage == null) return;
            
            UnSubscribeStorageModifiedEvent(storage);
            _slotChangedRegistry.UnRegister(storage);
            if (storage is IMutableCapacity cStorage)
                _capacityRegistry.UnRegister(cStorage);
            if (storage is IUsableItemStorage uStorage)
                _tryUsedRegistry.UnRegister(uStorage);

        }
        
        
        private Health playerHealth;
        private void RenewPlayerReference(Scene s, LoadSceneMode m) { RenewPlayerReference(); } // on
        private void RenewPlayerReference()
        {
            var old = pEquipHolder;
            IEquipmentHolder newer = null;
            
            if (FindFirstObjectByType<PlayerController>() is {} player 
                && player.TryGetComponent(out IEquipmentHolder newEquipHolder))
            {
                player.TryGetComponent<Health>(out playerHealth);
                newer = newEquipHolder;
            }
            
            if (old != null && old != newer)
                UnBindStorageEvents(old);

            pEquipHolder = newer;
            BindStorageEvents(pEquipHolder);
        }

        private void LoadItemTooltipUI()
        {
            // 아이템 툴팁 UI 로드
            if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
            {
                itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
                itemTooltip.HideTooltip();
            }
        }

        #endregion

        #region Un/Subscribe Event

        // Input Events (UI - View)
        private EventHandlerRegistry<IHoverableStorageUI, int> _hoverEnterRegistry;
        private EventHandlerRegistry<IHoverableStorageUI, int> _hoverExitRegistry;
        private EventHandlerRegistry<IClickableStorageUI, int> _clickRegistry;
        private EventHandlerRegistry<ISubClickableStorageUI, int> _subClickRegistry;
        
        private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
        {
            _hoverEnterRegistry.Register(sourceUI, (index) => OnSlotHovered(sourceUI, index));
        }

        private void SubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
        {
            _hoverExitRegistry.Register(sourceUI, (index) => OffSlotHovered(sourceUI, index));
        }

        private void SubscribeClickEvent(IClickableStorageUI sourceUI)
        {
            _clickRegistry.Register(sourceUI, (index) => OnSlotClicked(sourceUI, index));
        }
        
        private void SubscribeSubClickEvent(ISubClickableStorageUI sourceUI)
        {
            _subClickRegistry.Register(sourceUI, (index) => OnSlotSubClicked(sourceUI, index));
        }
        
        // Storage Events (Model)
        private EventHandlerRegistry<IGameItemStorage, IGameItemSlot> _slotChangedRegistry = null;
        private EventHandlerRegistry<IUsableItemStorage, IGameItemSlot> _tryUsedRegistry = null;
        private EventHandlerRegistry<IMutableCapacity, int> _capacityRegistry = null;
        
        private void SubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            _slotChangedRegistry.Register(storage, (slot) => OnSlotItemChanged(storage, slot));
        }

        private void SubscribeStorageCapacityEvent(IMutableCapacity storage)
        {
            _capacityRegistry.Register(storage, (capacity) => OnStorageCapacityChanged(storage, capacity));
        }

        private void SubscribeStorageUsageEvent(IUsableItemStorage storage)
        {
            _tryUsedRegistry.Register(storage, (slot) => OnSlotItemTryUsed(storage, slot));
        }

        private readonly Dictionary<IGameItemStorage, Action> _storageChangedHandlers = new();
        
        private void SubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            UnSubscribeStorageModifiedEvent(storage);
            Action handler = () => RefreshStorageUI(storage);
            _storageChangedHandlers[storage] = handler;
            storage.OnStorageChanged += handler;
        }

        private void UnSubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            if (_storageChangedHandlers.Remove(storage, out var handler))
                storage.OnStorageChanged -= handler;
        }
        
        private void ClearStorageModifiedEvent()
        {
            foreach (var (storage, handler) in _storageChangedHandlers)
            {
                if (storage != null)
                    storage.OnStorageChanged -= handler;
            }
        }
        
        
        #endregion

        #region Handle Storage Event

        private void OnSlotItemChanged(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (GetUIFromStorage(storage) is not { } ui) return;
            if (slot == null) return;
            
            CancelModifiedSlotProgress(slot);
            ui.DrawSlot(slot.Index, slot.GetItem);
        }

        private void OnStorageCapacityChanged(IMutableCapacity storage, int capacity)
        {
            if (GetUIFromStorage(storage) is not IMutableCapacityStorageUI storageUI) return;
            UpdateStorageUICapacity(storageUI, capacity);
        }
        
        private void OnSlotItemTryUsed(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (slot is not { HasItem: true, GetItem: { } item, GetItemInfo: { } itemData }) return;
            if (!itemData.isUsable) return;

            IGameItemStorage dest = storage == pStorage ? pEquipHolder : pStorage;
            
            switch (item.Type)
            {
                case Enums.ItemType.Countable:
                    itemConsumer.TryConsume(storage, slot, playerHealth, 1);
                    break;
                case Enums.ItemType.Equipment:
                    itemTransfer.TransferOrSwap(storage, slot, dest);
                    break;
                default:
                    break;
            }
            
            if (GetUIFromStorage(storage) is IHighlightableStorageUI hStorageUI)
            {
                hStorageUI.HighlightSlot(slot.Index, (int)SlotHighlightType.Modified);
                hStorageUI.UnHighlightSlotWithFade(slot.Index, (int)SlotHighlightType.Modified);
            }
        }

        #endregion

        #region Handle Input Event (UI - Slot)

        private void OnSlotHovered(IHoverableStorageUI target, int index)
        {
            Logg.Log($"[{GetType().Name}] OnSlotHovered({target}, {index})", Logg.LoggingMode.Completed);
            
            // 드래그 중이면 해당 슬롯에 저장 가능한지 체크하여 경고 하이라이트 표시
            if (isDragging && dragSourceSlot != null)
            {
                if (GetStorageFromUI(target) is { } targetStorage &&
                    targetStorage.TryGetItemSlot(index, out var targetSlot))
                {
                    // 드래그 중인 아이템을 해당 슬롯에 저장할 수 있는지 확인
                    if (!targetSlot.CanStore(dragSourceSlot.GetItemInfo))
                    {
                        // 저장 불가능한 경우 경고 하이라이트 표시
                        if (target is IHighlightableStorageUI highlightStorageUI)
                        {
                            highlightStorageUI.HighlightSlot(index, (int)SlotHighlightType.Warn);
                        }
                        return; // 경고 하이라이트만 표시하고 일반 hover 처리는 하지 않음
                    }
                }
            }
            
            if (lastHovered.Source == target && index == lastHovered.Index) return; // 동일 슬롯은 무시
            if (lastHovered.IsValid())
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false); // 기존 하이라이트된 슬롯 하이라이트 비활성화
            SetHighlightSlot(source: target, index, true); // 새 슬롯 하이라이트 활성화
            // 현재 호버 슬롯 정보 갱신
            lastHovered.Source = target;
            lastHovered.Index = index;
            // 아이템이 있는 슬롯일 경우 아이템 툴팁 출력
            if (GetStorageFromUI(target) is { } result &&
                result.TryGetItemSlot(index, out var slot) &&
                slot is {HasItem: true, IsAccessible: true})
            {
                itemTooltip.MoveTooltip(InputManager.Instance.PointerPos); 
                itemTooltip.ShowTooltip(slot);
            }
            else itemTooltip.HideTooltip(); // 아이템이 없는 슬롯일 경우 툴팁 비활성화
        }

        private void OffSlotHovered(IHoverableStorageUI targetUI, int index)
        {
            // 기존에 다른 슬롯이 lastHovered에 기록되어있는 경우
            // 해당 슬롯도 하이라이트 비활성화
            Logg.Log($"[{GetType().Name}] OffSlotHovered({targetUI}, {index})", Logg.LoggingMode.Completed);
            
            // 드래그 중이고 경고 하이라이트가 표시된 경우 제거
            if (isDragging && targetUI is IHighlightableStorageUI highlightStorageUI)
            {
                highlightStorageUI.UnHighlightSlot(index, (int)SlotHighlightType.Warn);
            }
            
            if (lastHovered.IsValid() && !lastHovered.Equals(targetUI, index)) 
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false); 
            // 타겟 슬롯(targetUI) 하이라이트 비활성화
            SetHighlightSlot(targetUI, index, false); 
            // 호버링 슬롯 기록 초기화
            lastHovered.Clear(); 
            itemTooltip.HideTooltip();
        }

        private void OnSlotClicked(IClickableStorageUI targetUI, int index)
        {
            if (GetStorageFromUI(targetUI) is not { } storage) return;
            if (!storage.TryGetItemSlot(index, out var slot)) return;
            
            ShowDetailedTooltip(storage, slot);
        }

        private void OnSlotSubClicked(ISubClickableStorageUI targetUI, int index)
        {
            if (GetStorageFromUI(targetUI) is not { } storage) return;
            if (!storage.TryGetItemSlot(index, out var slot)) return;
            if (slot.GetItemInfo is not {isUsable: true}) return;
            
            HandleItemUse(storage, slot);
        }

        private void OnDragStarted(IDraggableStorageUI sourceUI, int slotIndex)
        {
            if (GetStorageFromUI(sourceUI) is not { } storage) // UI로부터 스토리지를 찾을 수 없거나
            {
                Logg.LogError($"[InventoryController] OnDragStarted() - failed to find storage from ui {sourceUI}");
                return;
            }
            
            if (!storage.TryGetItemSlot(slotIndex, out var slot) ||  // 슬롯을 찾을 수 없거나
                !slot.HasItem) // 해당 슬롯이 비어있다면
            {
                pInvenUI.CancelDrag(); // 드래그 취소
                ClearDragState(); // 드래그 상태 초기화
                return;
            }

            // 드래그 상태 저장
            isDragging = true;
            dragSourceStorage = storage;
            dragSourceSlot = slot;

            // Equipment 타입 아이템이면 저장 가능한 장비 슬롯 하이라이트
            if (slot.GetItemInfo is { itemType: Enums.ItemType.Equipment })
            {
                HighlightEquipmentSlots(slot.GetItemInfo);
            }

            pInvenUI.AllowDrag(slot.GetItemInfo.sprite); // 드래그 허가 및 UI에게 필요한 시각적 효과 출력 명령
        }

        private void OnDragDrop(DragSlotInfo dragSlotInfo)
        {
            Logg.Log($"[InventoryController] DragDrop occured ({dragSlotInfo.From}, {dragSlotInfo.To})", 
                Logg.LoggingMode.Completed);
            
            var from = dragSlotInfo.From;
            var fromSource = from.source;
            var to = dragSlotInfo.To;
            var toSource = to.source;
            
            if (GetStorageFromUI(fromSource) is not { } fromStorage
                || !fromStorage.TryGetItemSlot(from.index, out var fromSlot))
            {
                // 드래그 상태 초기화
                ClearDragState();
                return;
            }
            
            pInvenUI.CancelDrag();
            
            // 드래그 상태 초기화
            ClearDragState();
            
            // 기존 로직: toStorage 확인 후 동일 스토리지 내부 정렬 또는 일반 전송/교환
            if (GetStorageFromUI(toSource) is not { } toStorage
                || !toStorage.TryGetItemSlot(to.index, out var toSlot))
                return;

            if (fromStorage == toStorage && fromStorage is IRearrangeableStorage rStorage)
            {
                Logg.Log($"[InventoryController] trying to intra swap ({fromSlot}, {toSlot})", 
                    Logg.LoggingMode.Completed);
                rStorage.TryTransferItem(fromSlot, toSlot);
            }
            else itemTransfer.TransferOrSwap(
                fromStorage, fromSlot, toStorage, toSlot);
        }        
        private void ClearDragState()
        {
            // 드래그 중이었던 아이템이 Equipment 타입이었다면 장비 슬롯 하이라이트 해제
            if (isDragging && dragSourceSlot != null && 
                dragSourceSlot.GetItemInfo is { itemType: Enums.ItemType.Equipment })
            {
                ClearEquipmentHighlights();
            }
            
            isDragging = false;
            dragSourceStorage = null;
            dragSourceSlot = null;
        }
        
        private void HighlightEquipmentSlots(ItemTypeSO draggedItemType)
        {
            if (pEquipHolder == null) return;
            if (pInvenUI.EquipmentUI is not IHighlightableStorageUI highlightUI) return;
            
            foreach (var slot in pEquipHolder.ItemSlots)
            {
                // 해당 슬롯에 드래그 중인 아이템을 저장할 수 있는지 확인
                if (slot.CanStore(draggedItemType))
                {
                    highlightUI.HighlightSlot(slot.Index);
                }
            }
        }
        private void ClearEquipmentHighlights()
        {
            if (pEquipHolder == null) return;
            if (pInvenUI.EquipmentUI is not IHighlightableStorageUI highlightUI) return;
            
            foreach (var slot in pEquipHolder.ItemSlots)
            {
                highlightUI.UnHighlightSlot(slot.Index);
            }
        }

        #endregion

        #region Handle Input Event (UI - Button)

        private void OnExitCalled()
        {
            UIManager.Instance.ClosePopupUI((PopupUI)pInvenUI);
        }

        private void OnFilterRequested(InventoryFilterType filter)
        {
            if (filter == currentFilter) return; // 현재 필터와 동일한 필터로 변경은 무시
            
            FilterStorage(pStorage, filter);
            currentFilter = filter;
            pInvenUI.UpdateFilter(filter);
        }

        private void OnInvenTrimRequested()
        {
            pStorage.Trim();
            RefreshStorageUI(pStorage);
        }

        private void OnInvenSortRequested()
        {
            pStorage.Sort();
            RefreshStorageUI(pStorage);
        }

        #endregion

        #region Control UI
        
        private void RefreshStorageUI(IGameItemStorage storage)
        {
            if (GetUIFromStorage(storage) is not { } storageUI) return;
            foreach (var slot in storage.ItemSlots)
            {
                var index = slot.Index;
                
                if (slot is {HasItem: true, GetItem: {} item })
                    storageUI.DrawSlot(index, item);
                else storageUI.CleanSlot(index);
                
                if (slot.IsVisible)
                    storageUI.ShowSlot(index);
                else storageUI.HideSlot(index);
            }
        }

        private void RefreshStorageUI()
        {
            RefreshStorageUI(pStorage);
        }
        
        private void RefreshEquipmentUI()
        {
            RefreshStorageUI(pEquipHolder);
        }

        private void UpdateStorageUICapacity(IMutableCapacityStorageUI storageUI, int capacity)
        {
            storageUI.SetCapacity(capacity);
        }

        private void SetHighlightSlot(object source, int index, bool state)
        {
            if (source is not IHighlightableStorageUI highUI) 
                return;
            
            if (state) 
                highUI.HighlightSlot(index);
            else highUI.UnHighlightSlot(index);
        }

        #endregion

        #region Control Storage

        private void HandleItemUse(IGameItemStorage storage, IGameItemSlot slot)
        {
            OnSlotItemTryUsed(storage, slot);
        }

        private void HandleItemDivide(IGameItemStorage storage, IGameItemSlot slot, LazyValue<int> expected)
        {
            if (expected is not { Initialized: true }) return;
            HandleItemDivide(storage, slot, expected.Value);
        }
        
        private void HandleItemDivide(IGameItemStorage storage, IGameItemSlot slot, int expected)
        {
            if (storage is not IDividableStorage dStorage) return;
            dStorage.TryDivide(slot.Index, expected);
        }

        private void FilterStorage(IGameItemStorage storage, InventoryFilterType filter)
        {
            if (storage.ItemSlots.Count == 0) return;
            foreach (var slot in storage.ItemSlots)
            {
                slot.SetVisibility(IsVisibleByFilter(slot, filter));
            }
            
            RefreshStorageUI(storage);
        }

        #endregion
        
        #region Helper Method

        private IGameItemStorage GetStorageFromUI(object targetUI)
        {
            if (targetUI == pInvenUI.StorageUI) return pStorage;
            if (targetUI == pInvenUI.EquipmentUI) return pEquipHolder;
            if (targetUI is QuickSlotPanelUI) return pQuickStorage;

            return null;
        }

        private IStorageUI GetUIFromStorage(object storage)
        {
            if (storage == pStorage) return pInvenUI.StorageUI;
            if (storage == pEquipHolder) return pInvenUI.EquipmentUI;
            
            return null;
        }
        
        private const string DefaultRemoveText = "버리기";
        private const string DefaultConsumeText = "사용";
        private const string DefaultEquipText = "장착";
        private const string DefaultUnEquipText = "장착해제";
        private const string DefaultDivideText = "나누기";

        private string GetUseButtonText(IGameItemStorage storage, ItemTypeSO itemInfo)
        {
            if (storage is IEquipmentHolder)
                return DefaultUnEquipText;
            if (itemInfo.itemType == Enums.ItemType.Equipment)
                return DefaultEquipText;
            return DefaultConsumeText;
        }
        
        private static bool IsVisibleByFilter(IGameItemSlot slot, InventoryFilterType filter)
        {
            return filter switch
            {
                InventoryFilterType.All => true,
                InventoryFilterType.Equipment => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Equipment } },
                InventoryFilterType.Consumable => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: true } }, 
                InventoryFilterType.Resource => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: false } }, 
                _ => false
            };
        }

        #endregion

        #region Sub Popup

        private void ShowDetailedTooltip(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item, GetItemInfo: {} itemInfo}) return;
            // 클로저 생성
            var targetStorage = storage;
            var targetSlot = slot; 
            var slotCTS = AddNewItemModifyProgress(targetSlot);
            // 팝업 출력 시도
            if (UIManager.Instance.ShowPopupUI<DetailedItemTooltipUI>() is not { } popup) return;
            // 팝업 CTS 체인 결합 및 내용 입력
            popup.ChainPopupCTS(slotCTS.Token);
            popup.SetTooltip(
                item: item,
                removeButton: new ButtonInfo(null,
                    itemInfo.itemType == Enums.ItemType.Special ? null : () => { ShowRemoveConfirmPopup(targetStorage, targetSlot); }),
                useButton: new ButtonInfo(GetUseButtonText(targetStorage, itemInfo),
                    itemInfo.isUsable ? () =>
                    {
                        HandleItemUse(targetStorage, targetSlot); // 아이템 사용 효과 처리 (소비/장착/장착해제 등)
                        if (popup is {} validPopup) validPopup.ClosePopupUI(); // 이후 팝업 닫기
                    } : null // 사용할 수 없는 아이템의 경우 사용 버튼 비활성화
                ),
                divideButton: new ButtonInfo<int>(DefaultDivideText,
                    itemInfo.itemType == Enums.ItemType.Countable ? (expected) =>
                    {
                        HandleItemDivide(targetStorage, targetSlot, expected); 
                        if (popup is {} validPopup) validPopup.ClosePopupUI(); // 이후 팝업 닫기
                    } : null // 개수 분리가 지원되지 않는 아이템의 경우 나누기 버튼 비활성화
                ) 
            );
        }

        private const string DefaultRemoveConfirmText = "아이템을 정말 파괴하시겠습니까?";

        private void ShowRemoveConfirmPopup(IGameItemStorage storage, IGameItemSlot slot)
        {
            // 클로저 생성
            var targetStorage = storage;
            var targetSlot = slot; 
            var slotCTS = AddNewItemModifyProgress(targetSlot);

            if (UIManager.Instance.ShowPopupUI<QuestionPopupUI>() is not { } popup) return;
            
            popup.ChainPopupCTS(slotCTS.Token);
            popup.SetQuestion(
                questionString: DefaultRemoveConfirmText,
                YesAction: () =>
                {
                    if (targetStorage == null || targetSlot == null) return; // 더이상 저장소와 슬롯 참조가 유효하지 않은 경우 취소
                    targetStorage.TryRemoveItem(targetSlot.Index); // todo: 아이템 제거에 실패한 경우 팝업을 닫는 대신 버리기 불가 안내
                    if (popup != null) popup.ClosePopupUI();
                },
                NoAction: () =>
                {
                    if (popup != null) popup.ClosePopupUI();
                });
        }
        
        #endregion
        
        #region Sub Popup CTS
        
        // 현재 유저가 상호작용 중인 (상세 팝업 호출, 아이템 버리기 팝업 호출 등) 작업 목록 <슬롯, CTS> 
        private readonly Dictionary<IGameItemSlot, CancellationTokenSource> progressingSlots = new ();

        private CancellationTokenSource AddNewItemModifyProgress(IGameItemSlot slot)
        {
            Logg.Log($"{nameof(AddNewItemModifyProgress)}: {slot}", Logg.LoggingMode.Completed);
            if (progressingSlots.TryGetValue(slot, out var cts) &&
                !(cts?.IsCancellationRequested ?? true))
            {
                return cts;
            }

            var newCTS = new CancellationTokenSource();
            progressingSlots[slot] = newCTS;

            return newCTS;
        }
        
        // 변동이 발생한 슬롯과 연결된 작업 및 팝업 취소
        private void CancelModifiedSlotProgress(IGameItemSlot slot)
        {
            if (slot == null) return;
            if (!progressingSlots.TryGetValue(slot, out var slotCTS)) return;
            
            ClearCTS(slotCTS); // Cancel and Dispose
            progressingSlots.Remove(slot);
        }

        // 모든 개별 슬롯과 연결된 작업 및 팝업 취소
        private void CancelAllSlotProgress()
        {
            if (progressingSlots.Count == 0) return;
            foreach (var cts in progressingSlots.Values)
            {
                ClearCTS(cts);
            }
        }
        
        private void ClearCTS(CancellationTokenSource tokenSource)
        {
            if (!tokenSource?.IsCancellationRequested ?? false)
                tokenSource.Cancel();
            tokenSource?.Dispose();
        }
        
        #endregion

        
        private void Refresh()
        {
            if (itemTooltip != null)
                itemTooltip.HideTooltip();
            CancelAllSlotProgress();
        }
    }
}



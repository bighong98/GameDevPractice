using System;
using System.Collections.Generic;
using System.Threading;
using RPG.Control;
using RPG.UI;
using TH.Core.Service;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using TH.Item.Storage;

namespace TH.Item
{
    public sealed class InventoryController: MonoBehaviour
    {
        // model
        private IPlayerStorage pStorage;
        private IEquipmentHolder pEquipHolder;
        // view
        private IPlayerInventoryUI pInvenUI;
        // sub popup
        private UI_ItemTooltip itemTooltip;
        private QuestionPopupUI removeConfirmPopup;

        private SlotUIInfo<IHoverableStorageUI> lastHovered;
        private InventoryFilterType currentFilter = InventoryFilterType.All;
        
        // outer service
        private IGameItemTransfer itemTransfer;

        
        private void Awake()
        {
            pStorage = ServiceLocator.Require<IPlayerStorage>();
            itemTransfer = ServiceLocator.Require<IGameItemTransfer>();
            
            if (!TryGetComponent(out pInvenUI))
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to GetComponent<{nameof(IPlayerInventoryUI)}>. disable inventory controller");
                this.enabled = false;
                return;
            }
            
            RenewPlayerReference();
            SceneManager.sceneLoaded += RenewPlayerReference;
            
            // 아이템 툴팁 UI 로드
            if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
            {
                itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
                itemTooltip.HideTooltip();
            }
        }

        private void OnEnable()
        {
            RenewPlayerReference(); // 플레이어의 EquipHolder 인스턴스 참조 및 이벤트 갱신
        }
        
        private void Start()
        {
            if (pInvenUI is not { StorageUI: { } storageUI, EquipmentUI: { } equipmentUI })
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to get {nameof(IPlayerStorageUI)}, {nameof(IEquipmentHolderUI)} from {nameof(IPlayerInventoryUI)}");
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
        }
        
        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
            
            UnBindStorageEvents(pStorage);
            UnBindStorageEvents(pEquipHolder);
            
            if (itemTooltip != null && itemTooltip.gameObject != null)
                Destroy(itemTooltip.gameObject);
            
            SceneManager.sceneLoaded -= RenewPlayerReference;
        }

        #region Initialization

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
         
            // 개별 Subscribe 계열 매서드들이 반드시 UnSubscribe 이후 구독하도록 할 것 (이벤트 누적 방지)
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
            UnSubscribeSlotModifiedEvent(storage);
            if (storage is IMutableCapacity cStorage)
                UnSubscribeStorageCapacityEvent(cStorage);
            if (storage is IUsableItemStorage uStorage)
                UnSubscribeStorageUsageEvent(uStorage);
        }
        
        
        private void RenewPlayerReference(Scene s, LoadSceneMode m) { RenewPlayerReference(); } // on
        private void RenewPlayerReference()
        {
            var old = pEquipHolder;
            IEquipmentHolder newer = null;
            
            if (FindFirstObjectByType<PlayerController>() is {} player 
                && player.TryGetComponent(out IEquipmentHolder newEquipHolder))
            {
                newer = newEquipHolder;
            }
            
            if (old != null && old != newer)
                UnBindStorageEvents(old);

            pEquipHolder = newer;
            BindStorageEvents(pEquipHolder);
        }

        #endregion

        #region Un/Subscribe Event

        // Input Events (UI - View)
        private readonly Dictionary<IHoverableStorageUI, Action<int>> _hoverEnterHandlers = new();
        private readonly Dictionary<IHoverableStorageUI, Action<int>> _hoverExitHandlers = new();
        private readonly Dictionary<IClickableStorageUI, Action<int>> _clickHandlers = new();
        private readonly Dictionary<ISubClickableStorageUI, Action<int>> _subClickHandlers = new();
        
        private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
        {
            UnSubscribeHoverEnterEvent(sourceUI);
            Action<int> e = (index) => OnSlotHovered(sourceUI, index);
            _hoverEnterHandlers[sourceUI] = e;
            sourceUI.OnSlotHovered += e;
        }

        private void SubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
        {
            UnSubscribeHoverExitEvent(sourceUI);
            Action<int> e = (index) => OffSlotHovered(sourceUI, index);
            _hoverExitHandlers[sourceUI] = e;
            sourceUI.OffSlotHovered += e;
        }

        private void SubscribeClickEvent(IClickableStorageUI sourceUI)
        {
            UnSubscribeClickEvent(sourceUI);
            Action<int> e = (index) => OnSlotClicked(sourceUI, index);
            _clickHandlers[sourceUI] = e;
            sourceUI.OnSlotClicked += e;
        }
        
        private void SubscribeSubClickEvent(ISubClickableStorageUI sourceUI)
        {
            UnSubscribeSubClickEvent(sourceUI);
            Action<int> e = (index) => OnSlotSubClicked(sourceUI, index);
            _subClickHandlers[sourceUI] = e;
            sourceUI.OnSlotSubClicked += e;
        }

        private void UnSubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
        {
            if (_hoverEnterHandlers.Remove(sourceUI, out var e))
                sourceUI.OnSlotHovered -= e;
        }
        private void UnSubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
        {
            if (_hoverExitHandlers.Remove(sourceUI, out var e))
                sourceUI.OffSlotHovered -= e;
        }

        private void UnSubscribeClickEvent(IClickableStorageUI sourceUI)
        {
            if (_clickHandlers.Remove(sourceUI, out var e))
                sourceUI.OnSlotClicked -= e;
        }

        private void UnSubscribeSubClickEvent(ISubClickableStorageUI sourceUI)
        {
            if (_subClickHandlers.Remove(sourceUI, out var e))
                sourceUI.OnSlotSubClicked -= e;
        }
        
        // Storage Events (Model)
        private readonly Dictionary<IGameItemStorage, Action> _storageChangedHandlers = new();
        private readonly Dictionary<IGameItemStorage, Action<IGameItemSlot>> _slotChangedHandlers = new();
        private readonly Dictionary<IUsableItemStorage, Action<IGameItemSlot>> _tryUsedHandlers = new();
        private readonly Dictionary<IMutableCapacity, Action<int>> _capacityHandlers = new();

        private void SubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            UnSubscribeStorageModifiedEvent(storage); // 중복 구독 방어
            Action e = () => RefreshStorageUI(storage);
            _storageChangedHandlers[storage] = e;
            storage.OnStorageChanged += e;
        }
        
        private void SubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            UnSubscribeSlotModifiedEvent(storage); // 기존 이벤트가 있다면 정리
            Action<IGameItemSlot> e = (slot) =>
            {
                OnSlotItemChanged(storage, slot);
            };
            _slotChangedHandlers[storage] = e;
            storage.OnSlotChanged += e;
        }

        private void SubscribeStorageCapacityEvent(IMutableCapacity storage)
        {
            UnSubscribeStorageCapacityEvent(storage); // 기존 이벤트가 있다면 정리
            Action<int> e = (capacity) => OnStorageCapacityChanged(storage, capacity);
            _capacityHandlers[storage] = e;
            storage.OnCapacityChanged += e;
        }

        private void SubscribeStorageUsageEvent(IUsableItemStorage storage)
        {
            UnSubscribeStorageUsageEvent(storage); // 기존 이벤트가 있다면 정리
            Action<IGameItemSlot> e = (slot) => OnSlotItemTryUsed(storage, slot);
            _tryUsedHandlers[storage] = e;
            storage.OnItemTryUsed += e;
        }
        
        private void UnSubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            if (_storageChangedHandlers.Remove(storage, out var e))
                storage.OnStorageChanged -= e;
        }
        
        private void UnSubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            if (_slotChangedHandlers.Remove(storage, out var e))
                storage.OnSlotChanged -= e;
        }

        private void UnSubscribeStorageCapacityEvent(IMutableCapacity storage)
        {
            if (_capacityHandlers.Remove(storage, out var e))
                storage.OnCapacityChanged -= e;
        }

        private void UnSubscribeStorageUsageEvent(IUsableItemStorage storage)
        {
            if (_tryUsedHandlers.Remove(storage, out var e))
                storage.OnItemTryUsed -= e;
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
                    itemTransfer.Consume(storage, dest, slot); // todo: 개수 적용
                    break;
                case Enums.ItemType.Equipment:
                    itemTransfer.TransferOrSwap(storage, slot, dest);
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Handle Input Event (UI - Slot)

        private void OnSlotHovered(IHoverableStorageUI target, int index)
        {
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
                itemTooltip.MoveTooltip(InputManager.Instance.PointerPos); //todo: 가능하면 UI에서 직접 받아오기
                itemTooltip.ShowTooltip(slot);
            }
            else itemTooltip.HideTooltip(); // 아이템이 없는 슬롯일 경우 툴팁 비활성화
        }

        private void OffSlotHovered(IHoverableStorageUI targetUI, int index)
        {
            if (lastHovered.IsValid() && !lastHovered.Equals(targetUI, index)) // 기존에 다른 슬롯이 호버링 상태로 기록되어있는 경우
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false); // 기록된 슬롯도 하이라이트 비활성화
            SetHighlightSlot(targetUI, index, false); // 타겟 슬롯 하이라이트 비활성화
            lastHovered.Clear(); // 호버링 슬롯 기록 초기화
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
                return;
            }

            pInvenUI.AllowDrag(slot.GetItemInfo.sprite); // 드래그 허가 및 UI에게 필요한 시각적 효과 출력 명령
        }

        private void OnDragDrop(DragSlotInfo dragSlotInfo)
        {
            Logg.Log($"[InventoryController] DragDrop occured ({dragSlotInfo.From}, {dragSlotInfo.To})", Logg.LoggingMode.Completed);
            
            var from = dragSlotInfo.From;
            var fromSource = from.source;
            var to = dragSlotInfo.To;
            var toSource = to.source;
            
            if (GetStorageFromUI(fromSource) is not { } fromStorage
                || !fromStorage.TryGetItemSlot(from.index, out var fromSlot))
                return;
            
            if (GetStorageFromUI(toSource) is not { } toStorage
                || !toStorage.TryGetItemSlot(to.index, out var toSlot))
                return;
            
            pInvenUI.CancelDrag();
            itemTransfer.TransferOrSwap(fromStorage, fromSlot, toStorage, toSlot);
        }
        
        

        #endregion

        #region Handle Input Event (UI - Button)

        private void OnExitCalled()
        {
            UIManager.Instance.ClosePopupUI((PopupUI)pInvenUI); // todo: 타입 캐스팅/체크 UIManager에서 처리하도록 변경
        }

        private void OnFilterRequested(InventoryFilterType filter)
        {
            if (filter == currentFilter) return; // 현재 필터와 동일한 필터로 변경은 무시
            
            FilterStorage(pStorage, filter);
            currentFilter = filter;
            pInvenUI.UpdateFilter(filter);
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
            if (source is not IHighlightableStorageUI highUI) return;
            
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
        
        private void CancelModifiedSlotProgress(IGameItemSlot slot)
        {
            if (slot == null) return;
            if (!progressingSlots.TryGetValue(slot, out var slotCTS)) return;
            
            ClearCTS(slotCTS); // Cancel and Dispose
            progressingSlots.Remove(slot);
        }

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

        private void Clear()
        {
            if (itemTooltip != null)
                itemTooltip.HideTooltip();
            CancelAllSlotProgress();
        }
    }
}



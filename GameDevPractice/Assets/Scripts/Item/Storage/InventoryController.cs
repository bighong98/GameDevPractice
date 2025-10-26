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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TH.Item
{
    [RequireComponent(typeof(IPlayerInventoryUI))]
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
        
        private IItemUsageHandler itemUsageHandler;

        
        private void Awake()
        {
            pStorage = ServiceLocator.Require<IPlayerStorage>();
            itemUsageHandler = ServiceLocator.Require<IItemUsageHandler>();
            
            if (!TryGetComponent(out pInvenUI))
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to GetComponent<{nameof(IPlayerInventoryUI)}>. disable inventory controller");
                this.enabled = false;
            }
            
            RenewPlayerReference();
            SceneManager.sceneLoaded += (_, _) => { RenewPlayerReference(); };
            
            // 아이템 툴팁 UI 로드
            if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
            {
                itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
                itemTooltip.HideTooltip();
            }
        }

        private void OnEnable()
        {
            pStorage.OnStorageChanged += RefreshStorageUI;
            pEquipHolder.OnStorageChanged += RefreshEquipmentUI;
        }

        private void OnDisable()
        {
            pStorage.OnStorageChanged -= RefreshStorageUI;
            pEquipHolder.OnStorageChanged -= RefreshEquipmentUI;
            
            Clear();
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
            SubscribeStorageCapacityEvent(pStorage);
            
            // UI(View) 이벤트 바인드
            BindStorageUIEvents(storageUI);
            BindEquipmentUIEvents(equipmentUI);
            BindButtonEvents();
            BindDragDropUIEvents();
            
            // 일회성 강제 갱신
            OnStorageCapacityChanged(pStorage, pStorage.Capacity);
            pInvenUI.UpdateFilter(currentFilter);
            RefreshStorageUI();
            RefreshEquipmentUI();
        }

        #region Initialization

        // View(UI) Event Bind
        private void BindStorageUIEvents(IPlayerStorageUI pStorageUI)
        {
            SubscribeHoverEnterEvent(pStorageUI);
            SubscribeHoverExitEvent(pStorageUI);
            SubscribeClickEvent(pStorageUI);
            SubscribeSubClickEvent(pStorageUI);
        }

        private void BindEquipmentUIEvents(IEquipmentHolderUI pEquipUI)
        {
            SubscribeHoverEnterEvent(pEquipUI);
            SubscribeHoverExitEvent(pEquipUI);
            SubscribeClickEvent(pEquipUI);
            SubscribeSubClickEvent(pEquipUI);
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
            SubscribeSlotModifiedEvent(storage);
        }
        
        private void RenewPlayerReference()
        {
            if (FindFirstObjectByType<PlayerController>() is {} player 
                && player.TryGetComponent(out IEquipmentHolder e))
            {
                pEquipHolder = e;
            }
        }

        #endregion

        #region Subscribe Event

        // Input Events (UI - View)
        private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
        {
            sourceUI.OnSlotHovered += (index) => { OnSlotHovered(sourceUI, index); };
        }

        private void SubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
        {
            sourceUI.OffSlotHovered += (index) => { OffSlotHovered(sourceUI, index); };
        }

        private void SubscribeClickEvent(IClickableStorageUI sourceUI)
        {
            sourceUI.OnSlotClicked += (index) => { OnSlotClicked(sourceUI, index); };
        }
        
        private void SubscribeSubClickEvent(ISubClickableStorageUI sourceUI)
        {
            sourceUI.OnSlotSubClicked += (index) => { OnSlotSubClicked(sourceUI, index); };
        }
        
        // Storage Events (Model)

        private void SubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            storage.OnSlotChanged += (slot) => { OnSlotItemChanged(storage, slot); };
        }

        private void SubscribeStorageCapacityEvent(IMutableCapacity storage)
        {
            storage.OnCapacityChanged += (capacity) => { OnStorageCapacityChanged(storage, capacity); };
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
            itemUsageHandler.TransferOrSwap(fromStorage, toStorage, fromSlot, toSlot);
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
            IGameItemStorage dest;
            if (storage == pStorage)
                dest = pEquipHolder;
            else dest = pStorage;
            
            switch (slot.GetItemInfo.itemType)
            {
                case Enums.ItemType.Equipment:
                    itemUsageHandler.Transfer(storage, dest, slot);
                    break;
                
                default: 
                    itemUsageHandler.Consume(storage, dest, slot); // todo: 개수 적용
                    break;
            }
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
        private const string DefaultDivideText = "개수 분리";

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
                divideButton: new ButtonInfo() //todo: 분리 기능 추가
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
            itemTooltip.HideTooltip();
            CancelAllSlotProgress();
        }
    }
}



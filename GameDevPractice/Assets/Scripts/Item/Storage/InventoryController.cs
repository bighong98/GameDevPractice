using System;
using System.Collections.Generic;
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
        private IPlayerInventory pInventory;
        private IEquipmentHolder pEquipHolder;
        // view
        private IPlayerInventoryUI pInvenUI;
        // sub popup
        private UI_ItemTooltip itemTooltip;
        private QuestionPopupUI removeConfirmPopup;

        private SlotUIInfo<IHoverableStorageUI> lastHovered;
        
        private IItemUsageHandler itemUsageHandler;

        
        private void Awake()
        {
            pInventory = ServiceLocator.Require<IPlayerInventory>();
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
            pInventory.OnStorageChanged += RefreshStorageUI;
            pEquipHolder.OnStorageChanged += RefreshEquipmentUI;
        }

        private void OnDisable()
        {
            pInventory.OnStorageChanged -= RefreshStorageUI;
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

            BindStorageEvents(pInventory);
            BindStorageEvents(pEquipHolder);
            
            BindStorageUIEvents(storageUI);
            BindEquipmentUIEvents(equipmentUI);
            BindButtonEvents();

            BindDragDropUIEvents();

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
        
        // Input Events (Storage - Model)

        private void SubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            storage.OnSlotChanged2 += (slot) => { OnSlotItemChanged(storage, slot); };
        }
        #endregion

        #region Handle Storage Event

        private void OnSlotItemChanged(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (GetUIFromStorage(storage) is not { } ui) return;
            if (slot == null) return;
            
            ui.DrawSlot(slot.Index, slot.GetItem);
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
            if (!storage.TryGetItem(index, out var item)) return;
            if (item.GetItemInfo is not { } itemInfo) return;
            //todo: 아이템 상세 툴팁 출력
            if (UIManager.Instance.ShowPopupUI<DetailedItemTooltipUI>() is {} popup)
            {
                //todo: 팝업 체인 결합
                //todo: 팝업 정보 등록 
            }
        }

        private void OnSlotSubClicked(ISubClickableStorageUI targetUI, int index)
        {
            if (GetStorageFromUI(targetUI) is not { } storage) return;
            if (!storage.TryGetItemSlot(index, out var slot)) return;
            if (slot.GetItemInfo is not {isUsable: true}) return;
            
            //todo: GetStorageFromUI 대신 출발, 도착 스토리지 반환하는 매서드 추가
            IGameItemStorage dest;
            if (storage == pInventory)
                dest = pEquipHolder;
            else dest = pInventory;
            
            
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

        #endregion

        private void RefreshStorageUI()
        {
            var pStorageUI = pInvenUI.StorageUI;
            foreach (var s in pInventory.ItemSlots)
            {
                if (s is { IsVisible: true, HasItem: true, Index: { } index, GetItem: { } item })
                {
                    pStorageUI.DrawSlot(index, item);
                }
            }
        }
        
        private void RefreshEquipmentUI()
        {
            var pEquipUI = pInvenUI.EquipmentUI;
            foreach (var s in pEquipHolder.ItemSlots)
            {
                if (s is { IsVisible: true, HasItem: true, Index: { } index, GetItem: { } item })
                {
                    pEquipUI.DrawSlot(index, item);
                }
            }
        }

        private void SetHighlightSlot(object source, int index, bool state)
        {
            if (source is not IHighlightableStorageUI highUI) return;
            
            if (state)
                highUI.HighlightSlot(index);
            else highUI.UnHighlightSlot(index);
        }

        private IGameItemStorage GetStorageFromUI(object targetUI)
        {
            if (targetUI == pInvenUI.StorageUI) return pInventory;
            if (targetUI == pInvenUI.EquipmentUI) return pEquipHolder;
            
            return null;
        }

        private IStorageUI GetUIFromStorage(object storage)
        {
            if (storage == pInventory) return pInvenUI.StorageUI;
            if (storage == pEquipHolder) return pInvenUI.EquipmentUI;

            return null;
        }

        private void RenewPlayerReference()
        {
            if (FindFirstObjectByType<PlayerController>() is {} player 
                && player.TryGetComponent(out IEquipmentHolder e))
            {
                pEquipHolder = e;
            }
        }

        private void Clear()
        {
            itemTooltip.HideTooltip();
        }
    }
}



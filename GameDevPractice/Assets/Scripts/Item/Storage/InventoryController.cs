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

        
        private void Awake()
        {
            pInventory = ServiceLocator.Require<IPlayerInventory>();
            
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
        }

        private void OnDisable()
        {
            pInventory.OnStorageChanged -= RefreshStorageUI;
        }

        private void Start()
        {
            if (pInvenUI is not { StorageUI: { } storageUI, EquipmentUI: { } equipmentUI })
            {
                Logg.LogError($"[{nameof(InventoryController)}] failed to get {nameof(IPlayerStorageUI)}, {nameof(IEquipmentHolderUI)} from {nameof(IPlayerInventoryUI)}");
                return;
            }
            
            ConnectStorageUIEvent(storageUI);
            ConnectEquipmentUIEvent(equipmentUI);
            RefreshStorageUI();
        }

        #region Initialization

        private void ConnectStorageUIEvent(IPlayerStorageUI pStorageUI)
        {
            SubscribeHoverEnterEvent(pStorageUI);
            SubscribeHoverExitEvent(pStorageUI);
            SubscribeClickEvent(pStorageUI);
        }

        private void ConnectEquipmentUIEvent(IEquipmentHolderUI pEquipUI)
        {
            SubscribeHoverEnterEvent(pEquipUI);
            SubscribeHoverExitEvent(pEquipUI);
            SubscribeClickEvent(pEquipUI);
        }

        #endregion

        #region Subscribe Input Event

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

        #endregion

        #region Handle Input Event

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
        }

        private void OffSlotHovered(IHoverableStorageUI targetUI, int index)
        {
            if (lastHovered.IsValid() && !lastHovered.Equals(targetUI, index)) // 기존에 다른 슬롯이 호버링 상태로 기록되어있는 경우
                SetHighlightSlot(lastHovered.Source, lastHovered.Index, false); // 기록된 슬롯도 하이라이트 비활성화
            SetHighlightSlot(targetUI, index, false); // 타겟 슬롯 하이라이트 비활성화
            lastHovered.Clear(); // 호버링 슬롯 기록 초기화
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

        private void SetHighlightSlot(object source, int index, bool state)
        {
            if (source is not IHighlightableStorageUI highUI) return;
            
            if (state)
                highUI.HighlightSlot(index);
            else highUI.UnHighlightSlot(index);
        }

        private IGameItemStorage GetStorageFromUI(object targetUI)
        {
            if (targetUI == pInvenUI.StorageUI)
            {
                return pInventory;
            }
            if (targetUI == pInvenUI.EquipmentUI)
            {
                return pEquipHolder;
            }

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
    }
}



using System;
using System.Collections;
using System.Collections.Generic;
using RPG.UI;
using TH.Item;
using TH.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TH.UI
{
    public class PlayerEquipmentUI : BaseUI, IEquipmentHolderUI, IPointerMoveHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IDropHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private List<EquipSlotUI> slots;
        IEnumerable IStorageUI.Slots => Slots;
        public IReadOnlyCollection<IEquipmentSlotUI> Slots { get { return readOnlySlots ??= slots.AsReadOnly(); } }
        private IReadOnlyCollection<IEquipmentSlotUI> readOnlySlots;
        
        public event Action<int> OnSlotHovered;
        public event Action<int> OffSlotHovered;
        public event Action<int> OnSlotClicked;
        public event Action<int> OnSlotSubClicked; 
        public event Action<int> OnSlotDragged;
        public event Action<int> OffSlotDragged;

        protected override void Awake()
        {
            base.Awake();
            
            InitSlotUIs();
        }
        
        private void InitSlotUIs() // todo: 실제 장비슬롯에 맞게 수정 필요
        {
            int idx = 0;
            foreach (var slot in slots)
            {
                slot.SetIndex(idx++);
            }
        }

        #region Draw/Show/Hide Slot (IStorageUI)
        public void DrawSlot(int index, IGameItem instance)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            if (instance is not IGameItem { GetItemInfo: { } itemInfo } item)
            {
                slotUI.Clear();
                return;
            }
            slotUI.SetIcon(itemInfo.sprite);
        }
        
        public void CleanSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Clear();
        }

        public void ShowSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.SetVisibility(true);
        }

        public void HideSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.SetVisibility(false);
        }

        #endregion
        
        #region Highlight (IHighlightableStorageUI)
        public void HighlightSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Highlight();
        }

        public void HighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Highlight(highlightType);
        }

        public void UnHighlightSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.UnHighlight();
        }

        public void UnHighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.UnHighlight(highlightType);
        }

        #endregion
        
        #region Helper Mehthods

        private bool IsValidSlotUIIdx(int index)
        {
            return index >= 0 && index < slots.Count;
        }
        private bool TryGetSlot(int index, out IEquipmentSlotUI slot)
        {
            if (!IsValidSlotUIIdx(index))
            {
                slot = null;
                return false;
            }

            slot = slots[index];
            return true;
        }

        #endregion
        
        #region Handle User Input

        private ISlotUI lastHoveredSlot;

        public void OnPointerMove(PointerEventData eventData)
        {
            switch (eventData.pointerEnter)
            {
                // 새로운 슬롯에 포인터가 이동한 경우
                case {} target when target.TryGetComponent(out ISlotUI slotUI) && slotUI != lastHoveredSlot:
                    if (lastHoveredSlot is {Index: {} lastHoveredIndex}) OffSlotHovered?.Invoke(lastHoveredIndex);
                    lastHoveredSlot = slotUI;
                    OnSlotHovered?.Invoke(slotUI.Index);
                    break;
                
                // 슬롯이 없는 빈 공간으로 이동했는데 기존에 머무르던 슬롯이 있는 경우
                case null when lastHoveredSlot != null:
                    OffSlotHovered?.Invoke(lastHoveredSlot.Index);
                    lastHoveredSlot = null;
                    break;
                
                default: // 기존 슬롯에서 머무르고 있거나 기존에 머무르던 슬롯이 없고 빈공간에 포인터가 위치해있는 경우
                    break;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (lastHoveredSlot == null) return;
            
            OffSlotHovered?.Invoke(lastHoveredSlot.Index);
            lastHoveredSlot = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Logg.Log($"[PlayerEquipmentUI] OnPointerClick '{eventData.pointerEnter}'", Logg.LoggingMode.Completed);
            if (eventData.dragging)
            {
                Logg.Log($"[PlayerEquipmentUI] OnPointerClick '{eventData.pointerEnter}' canceled because dragging is true", Logg.LoggingMode.Completed);
                return;
            }
            if (eventData.pointerEnter is { } target &&
                target.TryGetComponent(out ISlotUI slotUI))
            {
                switch (eventData.button)
                {
                    case PointerEventData.InputButton.Left when eventData.clickCount > 2:
                    case PointerEventData.InputButton.Right:
                        OnSlotSubClicked?.Invoke(slotUI.Index);
                        break;
                    default:
                        OnSlotClicked?.Invoke(slotUI.Index);
                        break;
                }
            } 
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            Logg.Log($"[PlayerEquipmentUI] OnBeginDrag invoked", Logg.LoggingMode.Completed);
            if (eventData.pointerEnter is { } target
                && target.TryGetComponent(out ISlotUI slotUI))
            {
                Logg.Log($"[PlayerEquipmentUI] OnBeginDrag trying to call OnSlotDragged.Invoke({slotUI})", Logg.LoggingMode.Completed);
                OnSlotDragged?.Invoke(slotUI.Index);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnDrop(eventData);
        }

        #endregion

        #region Drag&Drop

        public void OnDrop(PointerEventData eventData)
        {
            Logg.Log($"[PlayerEquipmentUI] OnEndDrag invoked", Logg.LoggingMode.Completed);

            if (eventData.pointerEnter is { } target
                && target.TryGetComponent(out ISlotUI slotUI))
            {
                Logg.Log($"[PlayerEquipmentUI] OnEndDrag trying to call OffSlotDragged.Invoke({slotUI})", Logg.LoggingMode.Completed);
                OffSlotDragged?.Invoke(slotUI.Index);
            }
            else
                OffSlotDragged?.Invoke(-1); // -1 means drop failed
        }

        public void OnDrag(PointerEventData eventData) { }

        private GameObject lastPointerDown;
        public void OnPointerDown(PointerEventData eventData)
        {
            lastPointerDown = eventData.pointerEnter;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (lastPointerDown != eventData.pointerEnter) return;
            
            OnPointerClick(eventData);
        }

        #endregion
    }
}


using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TH.UI;
using TH.Core.Pool;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

namespace TH.UI
{
    public class PlayerStorageUI : BaseUI, IPlayerStorageUI, IPointerMoveHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IDropHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private List<InvenSlotUI> slots;
        IEnumerable IStorageUI.Slots => Slots;
        public IReadOnlyCollection<IInvenSlotUI> Slots { get { return readOnlySlots ??= slots.AsReadOnly(); } }
        private IReadOnlyCollection<IInvenSlotUI> readOnlySlots;
        
        public event Action<int> OnSlotHovered;
        public event Action<int> OffSlotHovered;
        public event Action<int> OnSlotClicked;
        public event Action<int> OnSlotSubClicked;
        public event Action<int> OnSlotDragged;
        public event Action<int> OffSlotDragged;

        private GameObject slotUIPrefab;
        private ObjectPool<IPoolObject> slotUIPool;

        #region Enums

        enum GameObjects
        {
            InventoryArea,
            ItemSlots,
            DragDropIconHolder,
        }

        #endregion

        protected override void Awake()
        {
            base.Awake();
            BindObject(typeof(GameObjects));
            
            InitSlotUIs();
            InitSlotUIPool();
        }

        #region Initialization

        private void InitSlotUIs()
        {
            int idx = 0;
            foreach (var slot in slots)
            {
                slot.SetIndex(idx++);
            }
        }

        private const int DefaultSlotUIPoolCapacity = 100;
        private const int DefaultSlotUIPoolMax = 200;

        private void InitSlotUIPool()
        {
            ResourceManager.Instance.ReserveOperation(() => {
                if (ResourceManager.Instance.Load<GameObject>("InvenSlotUI")
                    is not { } loadedSlotUI) return;

                slotUIPrefab = loadedSlotUI;
                slotUIPool = PoolManager.Instance.GetPool(
                    slotUIPrefab,
                    GetObject((int)GameObjects.ItemSlots).transform,
                    capacity: DefaultSlotUIPoolCapacity,
                    maxSize: DefaultSlotUIPoolMax,
                    registerPool: false);
            });
        }

        #endregion
        
        #region Draw/Show/Hide Slot (IStorageUI)

        public void DrawSlot(int index, IGameItem instance)
        {
            // 슬롯UI, 아이템 객체 유효성 검사
            if (!TryGetSlot(index, out var slotUI)) return;
            if (instance is not { IsValid: true, GetItemInfo: { } itemInfo } )
            {
                slotUI.Clear();
                return;
            }
            // 슬롯UI 그리기
            slotUI.SetIcon(itemInfo.sprite);
            slotUI.SetAmount(instance.GetAmount);
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
            if (!TryGetSlot(index, out IInvenSlotUI slotUI)) return;
            slotUI.Highlight();
        }

        public void HighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out IInvenSlotUI slotUI)) return;
            slotUI.Highlight(highlightType);
        }

        public void UnHighlightSlot(int index)
        {
            if (!TryGetSlot(index, out IInvenSlotUI slotUI)) return;
            slotUI.UnHighlight();
        }

        public void UnHighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out IInvenSlotUI slotUI)) return;
            slotUI.UnHighlight(highlightType);
        }

        public void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f)
        {
            if (!TryGetSlot(index, out IInvenSlotUI slotUI)) return;
            slotUI.UnHighlightWithFade(highlightType, duration);
        }

        #endregion

        #region Helper Methods

        private bool IsValidSlotUIIdx(int index)
        {
            return index >= 0 && index < slots.Count;
        }
        
        private bool TryGetSlot(int index, out IInvenSlotUI slotUI)
        {
            if (!IsValidSlotUIIdx(index))
            {
                slotUI = null;
                return false;
            }

            slotUI = slots[index];
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
            Logg.Log($"[PlayerStorageUI] OnPointerClick '{eventData.pointerEnter}'", Logg.LoggingMode.Completed);
            if (eventData.dragging)
            {
                Logg.Log($"[PlayerStorageUI] OnPointerClick '{eventData.pointerEnter}' " +
                         $"canceled because dragging is true", Logg.LoggingMode.Completed);
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
            Logg.Log($"[PlayerStorageUI] OnBeginDrag invoked '{eventData.pointerDrag}'", Logg.LoggingMode.Completed);

            if (eventData.pointerEnter is { } target
                && target.TryGetComponent(out ISlotUI slotUI))
            {
                Logg.Log($"[PlayerStorageUI] OnBeginDrag trying to call OnSlotDragged.Invoke({slotUI})", 
                    Logg.LoggingMode.Completed);
                OnSlotDragged?.Invoke(slotUI.Index);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnDrop(eventData);
        }
        
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerEnter is { } target
                && target.TryGetComponent(out ISlotUI slotUI))
            {
                OffSlotDragged?.Invoke(slotUI.Index);
            }
            else
                OffSlotDragged?.Invoke(-1); // -1 means drop failed
        }

        public void OnDrag(PointerEventData eventData)
        {
            
        }

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
        
        public void SetCapacity(int capacity)
        {
            int length = slots.Count;
            if (capacity == length) return;
            if (capacity > length)
            {
                for (int i = 0; i < capacity - length; i++)
                {
                    if (slotUIPool.Get() is not InvenSlotUI slotUI) continue;
                    slotUI.SetIndex(i);
                    slots.Add(slotUI);
                }
            }
            else // case : capacity < length
            {
                for (int i = capacity; i < length; i++)
                {
                    HideSlot(i); // capacity 범위 밖 슬롯UI 비활성화
                }
            }
        }
    }
}


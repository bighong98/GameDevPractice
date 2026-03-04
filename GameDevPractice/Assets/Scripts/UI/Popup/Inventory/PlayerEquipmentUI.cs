using System;
using System.Collections;
using System.Collections.Generic;
using TH.UI;
using TH.Item;
using TH.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TH.UI
{
    // 장비 슬롯 UI 렌더링 및 입력 이벤트 중계 컴포넌트
    public class PlayerEquipmentUI : BaseUI, IEquipmentHolderUI, IPointerMoveHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IDropHandler, IPointerDownHandler, IPointerUpHandler
    {
        // 장비 슬롯 UI 리스트 참조
        [SerializeField] private List<EquipSlotUI> slots;
        IEnumerable IStorageUI.Slots => Slots;
        // 읽기 전용 슬롯 컬렉션 인터페이스
        public IReadOnlyCollection<IEquipmentSlotUI> Slots { get { return readOnlySlots ??= slots.AsReadOnly(); } }
        private IReadOnlyCollection<IEquipmentSlotUI> readOnlySlots;
        
        // 슬롯 입력 이벤트 노출 채널
        public event Action<int> OnSlotHovered;
        public event Action<int> OffSlotHovered;
        public event Action<int> OnSlotClicked;
        public event Action<int> OnSlotSubClicked; 
        public event Action<int> OnSlotDragged;
        public event Action<int> OffSlotDragged;

        // 슬롯 인덱스 초기화 진입점
        protected override void Awake()
        {
            base.Awake();
            
            InitSlotUIs();
        }
        
        // 인스펙터 슬롯 순서 기반 인덱스 할당
        private void InitSlotUIs() // todo: 실제 장비슬롯에 맞게 수정 필요
        {
            int idx = 0;
            foreach (var slot in slots)
            {
                slot.SetIndex(idx++);
            }
        }

        #region Draw/Show/Hide Slot (IStorageUI)
        // 슬롯 아이콘 렌더링
        public void DrawSlot(int index, IGameItem instance)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            if (instance is not IGameItem { GetItemInfo: { } itemInfo } item)
            {
                slotUI.Clear();
                return;
            }
            slotUI.SetIcon(itemInfo.Sprite);
        }
        
        // 슬롯 표시 내용 클리어
        public void CleanSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Clear();
        }

        // 슬롯 가시화
        public void ShowSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.SetVisibility(true);
        }

        // 슬롯 비가시화
        public void HideSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.SetVisibility(false);
        }

        #endregion
        
        #region Highlight (IHighlightableStorageUI)
        // 기본 하이라이트 적용
        public void HighlightSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Highlight();
        }

        // 타입 지정 하이라이트 적용
        public void HighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.Highlight(highlightType);
        }

        // 기본 하이라이트 해제
        public void UnHighlightSlot(int index)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.UnHighlight();
        }

        // 타입 지정 하이라이트 해제
        public void UnHighlightSlot(int index, int highlightType)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.UnHighlight(highlightType);
        }

        // 타입 지정 하이라이트 페이드 해제
        public void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f)
        {
            if (!TryGetSlot(index, out var slotUI)) return;
            slotUI.UnHighlightWithFade(highlightType, duration);
        }

        #endregion
        
        #region Helper Mehthods

        // 슬롯 인덱스 유효 범위 검증
        private bool IsValidSlotUIIdx(int index)
        {
            return index >= 0 && index < slots.Count;
        }

        // 슬롯 인덱스 기반 슬롯 UI 조회
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

        // 마지막 호버 슬롯 추적 캐시
        private ISlotUI lastHoveredSlot;

        // 포인터 이동 기반 슬롯 호버 상태 갱신
        public void OnPointerMove(PointerEventData eventData)
        {
            switch (eventData.pointerEnter)
            {
                // 새로운 슬롯에 포인터가 이동한 경우
                case {} target when target.TryGetComponent(out ISlotUI slotUI) && slotUI != lastHoveredSlot:
                    if (lastHoveredSlot is {Index: {} lastHoveredIndex}) 
                        OffSlotHovered?.Invoke(lastHoveredIndex); 
                        
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

        // 포인터 이탈 시 호버 상태 초기화
        public void OnPointerExit(PointerEventData eventData)
        {
            if (lastHoveredSlot == null) return;
            OffSlotHovered?.Invoke(lastHoveredSlot.Index);
            lastHoveredSlot = null;
        }

        // 포인터 클릭 입력 분기
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
        
        // 드래그 시작 슬롯 전달
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

        // 드래그 종료 시 드롭 로직 위임
        public void OnEndDrag(PointerEventData eventData)
        {
            OnDrop(eventData);
        }

        #endregion

        #region Drag&Drop

        // 드롭 대상 슬롯 인덱스 전달
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

        // 인터페이스 요구 빈 핸들러
        public void OnDrag(PointerEventData eventData) { }

        // 포인터 다운 대상 캐시
        private GameObject lastPointerDown;
        public void OnPointerDown(PointerEventData eventData)
        {
            lastPointerDown = eventData.pointerEnter;
        }

        // 포인터 업 시 클릭 판정 위임
        public void OnPointerUp(PointerEventData eventData)
        {
            if (lastPointerDown != eventData.pointerEnter) return;
            
            OnPointerClick(eventData);
        }

        #endregion
    }
}


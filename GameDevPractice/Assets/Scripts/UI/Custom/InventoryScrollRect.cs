using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TH.UI
{
    // UnityEngine.UI.ScrollRect 상속 커스텀 클래스
    // 인벤토리 내 아이템 드래그&드랍 중 스크롤 간섭 방지 목적
    public class InventoryScrollRect : ScrollRect, ICustomScrollRectHandler
    {
        private bool isDragEnabled = true;
        
        public void SetDragInteractable(bool state)
        {
            isDragEnabled = state;
        }

        // ScrollRect에서 구현하는 PointerEventData 기반 인터페이스 매서드 오버라이드
        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!isDragEnabled) return;
            base.OnBeginDrag(eventData);
        }
        
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragEnabled) return;
            base.OnEndDrag(eventData);
        }
        
        public override void OnDrag(PointerEventData eventData)
        {
            if (!isDragEnabled) return;
            base.OnDrag(eventData);
        }
    }
}


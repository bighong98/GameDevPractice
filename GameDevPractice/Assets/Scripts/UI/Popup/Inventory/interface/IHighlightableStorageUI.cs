using UnityEngine;

namespace TH.UI
{
    public interface IHighlightableStorageUI
    {
        void HighlightSlot(int index); // 인덱스 슬롯 강조 표시
        void HighlightSlot(int index, int highlightType); // 2개 이상의 강조 표시 방식이 존재할 경우 사용
        void UnHighlightSlot(int index); // 인덱스 슬롯 강조 표시 해제 (모든 강조 해제)
        void UnHighlightSlot(int index, int highlightType); // 인덱스 슬롯의 특정 강조 표시 제거
        void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f);
    }
}



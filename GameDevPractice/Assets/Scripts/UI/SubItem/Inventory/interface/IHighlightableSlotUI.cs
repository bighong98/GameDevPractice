using UnityEngine;

namespace TH.UI
{
    public interface IHighlightableSlotUI
    {
        void Highlight();
        void UnHighlight();
        void Highlight(int type);
        void UnHighlight(int type);
        void UnHighlightWithFade(int type, float duration = 0.5f);
    }

    public enum SlotHighlightType
    {
        Select,
        Warn,
        Modified,
    }
}


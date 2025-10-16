using UnityEngine;

namespace TH.UI
{
    public interface IHighlightableSlotUI
    {
        void Highlight();
        void UnHighlight();
        void Highlight(int type);
        void UnHighlight(int type);
    }
}


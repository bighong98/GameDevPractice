using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public interface ISlotUI
    {
        int Index { get; }
        
        Image IconImage { get; }
        Transform IconRect { get; }

        void SetIndex(int index);
        void SetVisibility(bool state);
        void SetIcon(Sprite sprite);
        void Clear();
    }
}


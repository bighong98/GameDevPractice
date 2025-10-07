using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public interface ISliderUIHandler
    {
        Slider GetSlider { get; }
        void SetFloor(float value);
        void SetCeil(float value);
        void UpdateBar();
        void OnHighlight();
        void OffHighlight();
    }
}


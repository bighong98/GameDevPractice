using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public interface ISliderUIHandler
    {
        Slider GetSlider { get; }
        void SetBaseline(float value);
        void SetFloor(float value);
        void SetCeil(float value);
        void Set(float floor, float ceil, float baseline = 0f);
        void UpdateBar();
        void OnHighlight();
        void OffHighlight();
        void Show();
        void Hide();
    }
}


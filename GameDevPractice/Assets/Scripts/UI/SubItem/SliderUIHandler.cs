using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class SliderUIHandler: ISliderUIHandler
    {
        private Slider slider;
        private float currFloor = 1f;
        private float currCeil = 1f;
        
        private TextMeshProUGUI text;
        
        public SliderUIHandler(Slider s)
        {
            slider = s;

            var parent = s.transform.parent.gameObject;
            if (Util.FindChild<TextMeshProUGUI>(parent, "text", recursive: true) is { } result)
            {
                text = result;
            }
        }
        
        public (float floor, float ceil) GetFloorAndCeil() => (currFloor, currCeil);
        public Slider GetSlider => slider;

        public void SetFloor(float value)
        {
            SetFloor(value, updateBar: true);
        }

        public void SetCeil(float value)
        {
            SetCeil(value, updateBar: true);
        }
        
        private void SetFloor(float value, bool updateBar)
        {
            currFloor = value;
            UpdateText();    
            
            if (updateBar)
                UpdateBar();
        }

        private void SetCeil(float value, bool updateBar)
        {
            currCeil = value;
            UpdateText();
            
            if (updateBar)
                UpdateBar();
        }

        public void SetFloorAndCeil(float floorValue, float ceilValue, bool updateBar = true)
        {
            if (!IsValidValue(floorValue, ceilValue)) return; 
            
            SetCeil(floorValue, updateBar: false);
            SetFloor(ceilValue, updateBar: false);

            if (updateBar)
                UpdateBar();
        }
        
        public void UpdateBar()
        {
            if (!IsValidValue(currFloor, currCeil)) return; 
            slider.value = Mathf.Clamp01(currFloor / currCeil);
        }

        private bool IsValidValue(float floor, float ceil)
        {
            // floor, ceil: 음수 불가
            // ceil: 0 불가
            // floor > ceil 불가
            return !(currFloor < 0f || currCeil <= 0f || currFloor > currCeil);
        }

        private void UpdateText()
        {
            text.SetText($"{currFloor} / {currCeil}");
        }

        private void ShowText()
        {
            UpdateText();
            text.enabled = true;
        }

        private void HideText()
        {
            text.enabled = false;
        }

        public void OnHighlight()
        {
            ShowText();
        }

        public void OffHighlight()
        {
            HideText();
        }
    }
}


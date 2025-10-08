using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class SliderUIHandler: ISliderUIHandler
    {
        private readonly Slider slider;
        private float currBaseline = 0f;
        private float currFloor = 1f;
        private float currCeil = 1f;
        
        private readonly TextMeshProUGUI text;
        
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

        public void SetBaseline(float value)
        {
            SetBaseline(value, updateBar: true);
        }
        
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
        
        private void SetBaseline(float value, bool updateBar)
        {
            currBaseline = value;
            UpdateText();

            if (updateBar)
                UpdateBar();
        }

        public void Set(float floor, float ceil, float baseline = 0f)
        {
            if (!IsValidValue(floor, ceil)) return; 
            
            SetCeil(floor, updateBar: false);
            SetFloor(ceil, updateBar: false);
            SetBaseline(baseline, updateBar: false);
            
            UpdateBar();
        }
        
        public void UpdateBar()
        {
            if (!IsValidValue(currFloor, currCeil)) return;
            float baseFloor = currFloor - currBaseline;
            float baseCeil = currCeil - currBaseline;
            if (!IsValidValue(baseFloor, baseCeil)) return;
            
            // slider.value = Mathf.Clamp01(currFloor / currCeil);
            slider.value = Mathf.Clamp01(baseFloor / baseCeil);
        }

        private bool IsValidValue(float floor, float ceil)
        {
            // floor, ceil: 음수 불가
            // ceil: 0 불가
            // floor > ceil 불가
            // return !(currFloor < 0f || currCeil <= 0f || currFloor > currCeil);
            return !(floor < 0f || ceil <= 0f || floor > ceil);
        }

        private void UpdateText()
        {
            text.SetText($"{currFloor - currBaseline} / {currCeil - currBaseline}");
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


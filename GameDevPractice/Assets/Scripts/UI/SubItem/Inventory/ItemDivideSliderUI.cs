using System;
using TH.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class ItemDivideSlider : MonoBehaviour, ISliderUIControllerInteger
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI amountText;

        public event Action<int> OnSliderValueConfirmed;
        private void Awake()
        {
            slider.wholeNumbers = true;
        }

        private void OnEnable()
        {
            ResetSubItems();
            SetSubItems();
        }

        private void OnDisable()
        {
            ResetSubItems();
        }

        private void OnDestroy()
        {
            ResetSubItems();
        }

        #region ISliderUIControllerInteger

        public void SetMinMax(int min, int max, int beginning)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = beginning;
            SetHandleText(slider.value);
        }
        
        public void Show()
        {
            if (gameObject.activeSelf) return;
            gameObject.SetActive(true);
        }
        
        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
        }

        #endregion
        
        private void SetSubItems()
        {
            if (button != null)
                button.onClick.AddListener(ConfirmValue);
            if (slider != null)
                slider.onValueChanged.AddListener(SetHandleText);
        }
        
        private void ConfirmValue()
        {
            int value = (int)slider.value;
            OnSliderValueConfirmed?.Invoke(value);
            Hide();
        }
        
        private void ResetSubItems()
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
            if (slider != null)
                slider.onValueChanged.RemoveAllListeners();
        }

        private void SetHandleText(float value)
        {
            if (amountText == null) return;
            amountText.SetText(((int)value).ToString());
        }
        
        
    }

    public interface ISliderUIControllerInteger
    {
        event Action<int> OnSliderValueConfirmed;
        void SetMinMax(int min, int max, int beginning);
        void Show();
        void Hide();
    }
}


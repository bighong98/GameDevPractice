using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundVolumeOptionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button muteButton;

    public void Initialize(string label, float initialValue, Action<float> onValueChanged, Action onMuteToggle)
    {
        if (labelText != null)
            labelText.text = label;

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.value = initialValue;
            if (onValueChanged != null)
                volumeSlider.onValueChanged.AddListener(value => onValueChanged(value));
        }

        if (muteButton != null)
        {
            muteButton.onClick.RemoveAllListeners();
            if (onMuteToggle != null)
                muteButton.onClick.AddListener(() => onMuteToggle());
        }
    }

    public void SetValueWithoutNotify(float value)
    {
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(value);
    }
}

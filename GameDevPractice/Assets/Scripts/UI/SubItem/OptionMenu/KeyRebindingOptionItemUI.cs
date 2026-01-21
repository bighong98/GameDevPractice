using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyRebindingOptionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text bindingText;
    [SerializeField] private Button rebindButton;
    [SerializeField] private Button resetButton;

    public void Initialize(string label, string bindingDisplay, Action onRebind, Action onReset)
    {
        if (labelText != null)
            labelText.text = label;

        if (bindingText != null)
            bindingText.text = bindingDisplay;

        if (rebindButton != null)
        {
            rebindButton.onClick.RemoveAllListeners();
            if (onRebind != null)
                rebindButton.onClick.AddListener(() => onRebind());
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            if (onReset != null)
                resetButton.onClick.AddListener(() => onReset());
        }
    }

    public void SetBindingText(string text)
    {
        if (bindingText != null)
            bindingText.text = text;
    }

    public void SetInteractable(bool interactable)
    {
        if (rebindButton != null)
            rebindButton.interactable = interactable;
        if (resetButton != null)
            resetButton.interactable = interactable;
    }
}

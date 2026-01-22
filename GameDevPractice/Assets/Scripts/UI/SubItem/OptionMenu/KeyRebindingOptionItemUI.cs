using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyRebindingOptionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text bindingText;
    [SerializeField] private Button rebindButton;
    [SerializeField] private Button unbindButton;
    [SerializeField] private Button defaultButton;

    public void Initialize(string label, string bindingDisplay, Action onRebind, Action onUnbind, Action onDefault)
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

        if (unbindButton != null)
        {
            unbindButton.onClick.RemoveAllListeners();
            if (onUnbind != null)
                unbindButton.onClick.AddListener(() => onUnbind());
        }

        if (defaultButton != null)
        {
            defaultButton.onClick.RemoveAllListeners();
            if (onDefault != null)
                defaultButton.onClick.AddListener(() => onDefault());
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
        if (unbindButton != null)
            unbindButton.interactable = interactable;
        if (defaultButton != null)
            defaultButton.interactable = interactable;
    }
}

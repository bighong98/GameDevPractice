using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private RectTransform selfRect;
    private Image image;

    private void Awake()
    {
        // Util.SetMainCameraForUtilClass();
        selfRect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (Util.IsInsideScreen(target.position, out var result))
        {
            selfRect.position = result;
            Show();
        }
        else
        {
            Util.Log($"not in screen");
            Hide();
        }
    }

    private void Show()
    {
        if (image.isActiveAndEnabled) return;
        image.enabled = true;
    }

    private void Hide()
    {
        if (!image.isActiveAndEnabled) return;
        image.enabled = false;
    }
}

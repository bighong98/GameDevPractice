using TH.Attribute.Stat;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusPanelSectionUI : MonoBehaviour
{
    [SerializeField] private GameStatCategory category;
    [SerializeField] private ScrollRect scrollRect;
    
    [SerializeField] private bool debugLayout = false;
[SerializeField] private RectTransform content;

    public GameStatCategory Category => category;
    public ScrollRect ScrollRect => scrollRect;
    public RectTransform Content => content;

    public RectTransform EnsureAndGetContent()
    {
        EnsureScrollSetup();
        return content;
    }

    public void EnsureScrollSetup()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelSectionUI)}] ScrollRect is missing on {name}");
            return;
        }

        var scrollRectRect = scrollRect.GetComponent<RectTransform>();
        EnsureStretchToParent(scrollRectRect);
        EnsureRectSize(scrollRectRect);

        if (scrollRect.viewport == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelSectionUI)}] Viewport is missing on {name}");
            return;
        }

        var viewportRect = scrollRect.viewport;
        EnsureStretchToParent(viewportRect);
        EnsureRectSize(viewportRect);

        if (content == null)
            content = scrollRect.content;

        if (content == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelSectionUI)}] Content is missing on {name}");
            return;
        }

        EnsureContentAnchors(content);
        EnsureRectSize(content);
        scrollRect.content = content;

        if (debugLayout)
        {
            var parentRect = scrollRectRect != null ? scrollRectRect.parent as RectTransform : null;
            Logg.Log($"[{nameof(PlayerStatusPanelSectionUI)}] Layout '{name}' scrollRect={scrollRectRect?.rect.size}, viewport={viewportRect.rect.size}, content={content.rect.size}, parent={parentRect?.rect.size}");
        }
    }

    private void EnsureStretchToParent(RectTransform rect)
    {
        if (rect == null || rect.parent == null) return;
        if (rect.rect.size != Vector2.zero) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void EnsureRectSize(RectTransform rect)
    {
        if (rect == null || rect.parent == null) return;
        if (rect.rect.size != Vector2.zero) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        if (rect.rect.size != Vector2.zero) return;

        var ancestor = rect.parent as RectTransform;
        while (ancestor != null && ancestor.rect.size == Vector2.zero)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(ancestor);
            if (ancestor.rect.size != Vector2.zero) break;
            ancestor = ancestor.parent as RectTransform;
        }

        if (ancestor != null && ancestor.rect.size != Vector2.zero)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ancestor.rect.size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ancestor.rect.size.y);
            return;
        }

        var rootCanvas = rect.GetComponentInParent<Canvas>(true);
        var rootRect = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : null;
        if (rootRect != null && rootRect.rect.size != Vector2.zero)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rootRect.rect.size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rootRect.rect.size.y);
        }
    }


    private void EnsureContentAnchors(RectTransform rect)
    {
        if (rect == null) return;
        if (rect.rect.size != Vector2.zero) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
    }


}

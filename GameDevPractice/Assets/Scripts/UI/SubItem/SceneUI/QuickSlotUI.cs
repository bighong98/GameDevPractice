using System.Collections;
using TH.UI;
using TH.Utils;
using UnityEngine;

public class QuickSlotUI : BaseSlotUI, IHighlightableSlotUI
{
    #region Enums

    enum TMPTexts
    {
        ItemAmountText,
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        BindTMPText(typeof(TMPTexts));
    }

    public void SetAmount(int amount)
    {
        if (GetTMPText((int)TMPTexts.ItemAmountText) is not {} t)
        {
            Logg.LogError($"[{gameObject.name}] InvenSlotUI failed to find {TMPTexts.ItemAmountText}");
            return;
        }
        
        t.SetText(amount.ToString());
        t.enabled = true;
    }

    new public void HideIcon()
    {
        base.HideIcon();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
    }

    public override void Clear()
    {
        base.Clear();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
        UnHighlight();
    }

    #region IHighlightableSlotUI
    private Coroutine _fadeCoroutine;
    private int currentHighlightType;
    public override void Highlight() => Highlight((int)SlotHighlightType.Select);

    public void Highlight(int type)
    {
        if (GetImage((int)Images.HighLightImage) is {} highlightImage && highlightImage.IsAlive())
        {
            var color = type switch
            {
                (int)SlotHighlightType.Modified => Color.yellow,
                (int)SlotHighlightType.Warn => Color.red,
                _ => Color.green
            };
            color.a = 0.25f;
            highlightImage.color = color;
        }
        currentHighlightType = type;
        base.Highlight();
    }

    public void UnHighlight(int type)
    {
        if (currentHighlightType != type) return;
        currentHighlightType = -1;
        base.UnHighlight();
    }

    public void UnHighlightWithFade(int type, float duration = 0.5f)
    {
        if (currentHighlightType != type) return;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        _fadeCoroutine = StartCoroutine(CoUnHighlightFade(type, duration));
    }

    private IEnumerator CoUnHighlightFade(int type, float duration)
    {
        if (GetImage((int)Images.HighLightImage) is not {} highlightImage || !highlightImage.IsAlive())
            yield break;

        float elapsed = 0f;
        Color color = highlightImage.color;
        float startAlpha = color.a;

        // duration 동안 fade out
        while (elapsed < duration && currentHighlightType == type)
        {
            elapsed += Time.unscaledDeltaTime; // 일시정지와 무관하게 동작
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            highlightImage.color = color;

            yield return null;
        }
        // 중간에 하이라이트 타입이 바뀌지 않았다면 하이라이트 해제
        if (currentHighlightType == type)
        {
            UnHighlight(type);
        }

        _fadeCoroutine = null;
    }

    #endregion
}

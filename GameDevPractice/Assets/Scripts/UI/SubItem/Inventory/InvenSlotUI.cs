using System;
using System.Collections;
using TH.UI;
using TH.Utils;
using TMPro;
using UnityEngine;

public sealed class InvenSlotUI : BaseSlotUI, IInvenSlotUI
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

    private void OnDisable()
    {
        if (_fadeCoroutine == null) return;
        
        StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
    }

    public void SetAmount(int amount)
    {
        if (GetTMPText((int)TMPTexts.ItemAmountText) is not {} t)
        {
            Logg.LogError($"[{gameObject.name}] InvenSlotUI failed to find {TMPTexts.ItemAmountText.ToString()}");
            return;
        }

        if (amount <= 1)
        {
            t.enabled = false;
            return;
        }
        
        t.SetText(amount.ToString());
        t.enabled = true;
    }

    protected override void HideIcon()
    {
        base.HideIcon();
        GetTMPText((int)TMPTexts.ItemAmountText).enabled = false;
    }

    #region IHighlightableSlotUI
    
    private Coroutine _fadeCoroutine;
    private int currentHighlightType;
    public override void Highlight() => Highlight((int)SlotHighlightType.Select);

    public void Highlight(int type)
    {
        if (GetImage((int)Images.HighLightImage) is {} highlightImage && highlightImage.IsNotNull())
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
        // if (!gameObject.activeSelf || !gameObject.activeInHierarchy) return;
        if (!isActiveAndEnabled) return;
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
        if (GetImage((int)Images.HighLightImage) is not {} highlightImage || !highlightImage.IsNotNull())
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

    public override void Clear()
    {
        base.Clear();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
    }


}

using System.Collections;
using TH.Resource;
using TH.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveSkillSlotUI : BaseSlotUI, IHighlightableSlotUI
{
    [SerializeField] private Image cooldownImage;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TextMeshProUGUI slotKeyText;

    private Coroutine fadeCoroutine;
    private int currentHighlightType = -1;
    private SkillTypeSO boundSkill;

    protected override void Awake()
    {
        base.Awake();
        InitializeUIState();
    }

    public void SetSkill(SkillTypeSO skill)
    {
        boundSkill = skill;
        if (boundSkill == null)
        {
            Clear();
            return;
        }

        ApplySkillIcon(boundSkill.SkillSlotImage);
        SetCooldown(0f, boundSkill.Cooldown);
    }

    public void SetCooldown(float remainingCooldown, float totalCooldown)
    {
        if (boundSkill == null)
        {
            DisableSkillVisuals();
            return;
        }

        float resolvedRemaining = Mathf.Max(0f, remainingCooldown);
        float resolvedTotal = Mathf.Max(0f, totalCooldown);

        if (cooldownImage != null)
        {
            float fillAmount = resolvedTotal > 0f
                ? Mathf.Clamp01(resolvedRemaining / resolvedTotal)
                : 0f;

            cooldownImage.fillAmount = fillAmount;
            cooldownImage.enabled = fillAmount > 0f;
        }

        if (cooldownText == null)
            return;

        if (resolvedRemaining > 0f)
        {
            cooldownText.enabled = true;
            cooldownText.SetText($"{resolvedRemaining:0.0}");
            return;
        }

        cooldownText.enabled = false;
        cooldownText.SetText(string.Empty);
    }

    public void SetSlotKeyText(string text)
    {
        if (slotKeyText == null)
            return;

        bool hasText = !string.IsNullOrWhiteSpace(text);
        slotKeyText.enabled = hasText;
        slotKeyText.SetText(hasText ? text : string.Empty);
    }

    public override void Clear()
    {
        base.Clear();
        boundSkill = null;

        DisableSkillVisuals();
        SetSlotKeyText(string.Empty);
        UnHighlight();
    }

    public override void Highlight() => Highlight((int)SlotHighlightType.Select);

    public override void UnHighlight()
    {
        StopHighlightFade();
        currentHighlightType = -1;
        base.UnHighlight();
    }

    public void Highlight(int type)
    {
        if (GetImage((int)Images.HighLightImage) is { } highlightImage)
        {
            Color highlightColor = type switch
            {
                (int)SlotHighlightType.Modified => Color.yellow,
                (int)SlotHighlightType.Warn => Color.red,
                _ => Color.green,
            };
            highlightColor.a = 0.25f;
            highlightImage.color = highlightColor;
        }

        currentHighlightType = type;
        base.Highlight();
    }

    public void UnHighlight(int type)
    {
        if (currentHighlightType != type)
            return;

        currentHighlightType = -1;
        base.UnHighlight();
    }

    public void UnHighlightWithFade(int type, float duration = 0.5f)
    {
        if (currentHighlightType != type)
            return;

        StopHighlightFade();
        fadeCoroutine = StartCoroutine(CoUnHighlightFade(type, duration));
    }

    private void InitializeUIState()
    {
        DisableSkillVisuals();
        base.UnHighlight();

        if (slotKeyText != null)
            slotKeyText.enabled = false;
    }

    private void ApplySkillIcon(Sprite iconSprite)
    {
        if (GetImage((int)Images.ItemImage) is not { } itemImage)
            return;

        itemImage.sprite = iconSprite;
        itemImage.enabled = iconSprite != null;
    }

    private void DisableSkillVisuals()
    {
        ApplySkillIcon(null);

        if (cooldownImage != null)
        {
            cooldownImage.fillAmount = 0f;
            cooldownImage.enabled = false;
        }

        if (cooldownText != null)
        {
            cooldownText.enabled = false;
            cooldownText.SetText(string.Empty);
        }
    }

    private IEnumerator CoUnHighlightFade(int type, float duration)
    {
        if (GetImage((int)Images.HighLightImage) is not { } highlightImage)
            yield break;

        float elapsed = 0f;
        Color color = highlightImage.color;
        float startAlpha = color.a;

        while (elapsed < duration && currentHighlightType == type)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            highlightImage.color = color;
            yield return null;
        }

        if (currentHighlightType == type)
            UnHighlight(type);

        fadeCoroutine = null;
    }

    private void StopHighlightFade()
    {
        if (fadeCoroutine == null)
            return;

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}


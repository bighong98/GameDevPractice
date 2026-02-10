using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.UI.Data;
using TH.Utils;
using TMPro;
using UnityEngine;
using TH.Core.Service;

public class FloatingTextController : MonoBehaviour, IPoolObject, IFloatingTextController
{
    [SerializeField] private TextMeshProUGUI text;
    [Header("Animation")]
    [SerializeField] private FloatingTextSO animationData;

    [Header("Canvas Tuning")]
    [SerializeField] private float canvasFontScale = 12f;
    [SerializeField] private float minCanvasFontSize = 18f;
    
    [Header("Batch Layout")]
    [SerializeField] private float batchLineHeightMultiplier = 0.9f;
    [SerializeField] private float batchHorizontalStep = 18f;
    [SerializeField] private int maxBatchEntries = 6;
    [SerializeField] private float batchItemStaggerDelay = 0.04f;

    private readonly List<TextMeshProUGUI> textItems = new();
    private RectTransform rootRect;
    private Canvas rootCanvas;
    private RectTransform rootCanvasRect;
    private Transform worldAnchor;
    private Vector3 worldPosition;
    private Vector3 worldOffset;
    private IFloatingTextData AnimationData => animationData;

    private CancellationTokenSource _animCts;
    private Camera worldCamera;
    private CanvasGroup canvasGroup;
    private FloatingTextBatchLayout currentBatchLayout = FloatingTextBatchLayout.Spread;
    private readonly List<int> batchGroupKeys = new();
    private bool useGroupedBatchLayout;
    private bool useBatchTimingStagger;
    private int visibleTextCount = 1;

    private void Awake()
    {
        if (text == null)
            text = Util.FindChild<TextMeshProUGUI>(gameObject, "text", true);
        if (text == null)
        {
            Logg.LogError($"[{nameof(FloatingTextController)}] failed to resolve text component");
            enabled = false;
            return;
        }

        rootRect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        textItems.Clear();
        textItems.Add(text);
        ResetTextItems();

        ResolveCanvasContext();
    }

    private void OnEnable()
    {
        worldCamera = Camera.main;
        ResolveCanvasContext();
    }

    public void Set(FloatingTextSO data, string textValue)
    {
        SetSetting(data);
        SetText(textValue);
    }

    public void SetSetting(FloatingTextSO data)
    {
        animationData = data;
    }

    public void SetText(string s)
    {
        if (text == null) return;

        currentBatchLayout = FloatingTextBatchLayout.Spread;
        useGroupedBatchLayout = false;
        useBatchTimingStagger = false;
        batchGroupKeys.Clear();
        EnsureTextItemCount(1);
        visibleTextCount = 1;
        ApplyTextValue(textItems[0], s);
        DeactivateTextItemsFrom(1);
        Refresh();
    }

    public void SetBatchTexts(IReadOnlyList<string> values)
    {
        SetBatchTexts(values, FloatingTextBatchLayout.Spread);
    }

    public void SetBatchTexts(IReadOnlyList<string> values, FloatingTextBatchLayout layout)
    {
        if (text == null) return;

        if (values == null || values.Count == 0)
        {
            SetText(string.Empty);
            return;
        }

        currentBatchLayout = layout;
        useGroupedBatchLayout = false;
        int count = Mathf.Clamp(values.Count, 1, Mathf.Max(1, maxBatchEntries));
        useBatchTimingStagger = count > 1;
        batchGroupKeys.Clear();
        EnsureTextItemCount(count);
        visibleTextCount = count;

        for (int i = 0; i < count; i++)
            ApplyTextValue(textItems[i], values[i]);

        DeactivateTextItemsFrom(count);
        Refresh();
    }

    public void SetBatchTexts(IReadOnlyList<string> values, IReadOnlyList<int> attackInstanceIds)
    {
        if (text == null) return;

        if (values == null || values.Count == 0)
        {
            SetText(string.Empty);
            return;
        }

        currentBatchLayout = FloatingTextBatchLayout.Spread;
        useGroupedBatchLayout = true;
        int count = Mathf.Clamp(values.Count, 1, Mathf.Max(1, maxBatchEntries));
        useBatchTimingStagger = count > 1;
        EnsureTextItemCount(count);
        visibleTextCount = count;

        batchGroupKeys.Clear();
        for (int i = 0; i < count; i++)
        {
            ApplyTextValue(textItems[i], values[i]);

            int attackInstanceId = (attackInstanceIds != null && i < attackInstanceIds.Count)
                ? attackInstanceIds[i]
                : 0;
            int groupKey = attackInstanceId > 0 ? attackInstanceId : int.MinValue + i;
            batchGroupKeys.Add(groupKey);
        }

        DeactivateTextItemsFrom(count);
        Refresh();
    }


    public void SetWorldAnchor(Transform anchor)
    {
        worldAnchor = anchor;
        if (worldAnchor != null)
            worldPosition = worldAnchor.position;
    }
    
    public void SetWorldAnchor(Transform anchor, Vector3 fallbackWorldPosition)
    {
        worldAnchor = anchor;
        if (worldAnchor != null)
            worldPosition = worldAnchor.position;
        else
            worldPosition = fallbackWorldPosition;
    }

    private void Refresh()
    {
        if (text == null || animationData == null) return;

        ApplySetting();
        for (int i = 0; i < visibleTextCount; i++)
        {
            var item = textItems[i];
            item.havePropertiesChanged = true;
            item.ForceMeshUpdate();
            item.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        CancelAnimationTask();
        StartFloatAndFadeTask();
    }

    private void ApplySetting()
    {
        worldOffset = AnimationData.StartOffset;
        float fontSize = ResolveFontSize(AnimationData.TextSize);
        Color color = AnimationData.TextColor;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        for (int i = 0; i < visibleTextCount; i++)
        {
            var item = textItems[i];
            item.fontSize = fontSize;
            item.color = color;
            item.enabled = true;
            item.gameObject.SetActive(true);
        }

        LayoutVisibleTextItems(fontSize);
        bool isVisibleOnScreen = UpdateScreenPosition();
        if (useBatchTimingStagger && isVisibleOnScreen)
        {
            ApplyStaggeredItemVisuals(
                0f,
                AnimationData.LifeTime,
                Mathf.Max(0f, AnimationData.LifeTime - AnimationData.FadeOutDuration),
                AnimationData.FadeOutDuration,
                Mathf.Max(0f, batchItemStaggerDelay));
        }
    }

    private float ResolveFontSize(float configuredSize)
    {
        return Mathf.Max(minCanvasFontSize, configuredSize * canvasFontScale);
    }
    
    private void LayoutVisibleTextItems(float fontSize)
    {
        float lineStep = Mathf.Max(1f, fontSize * batchLineHeightMultiplier);
        if (useGroupedBatchLayout)
        {
            LayoutGroupedBatchItems(lineStep);
            return;
        }

        if (currentBatchLayout == FloatingTextBatchLayout.Line)
        {
            for (int i = 0; i < visibleTextCount; i++)
            {
                var itemRect = textItems[i].rectTransform;
                float y = i * lineStep;
                itemRect.anchoredPosition = new Vector2(0f, y);
            }
            return;
        }

        for (int i = 0; i < visibleTextCount; i++)
        {
            var itemRect = textItems[i].rectTransform;
            float x = 0f;
            if (i > 0)
            {
                float spread = Mathf.Ceil(i * 0.5f);
                x = ((i & 1) == 1 ? 1f : -1f) * spread * batchHorizontalStep;
            }

            float y = i * lineStep;
            itemRect.anchoredPosition = new Vector2(x, y);
        }
    }

    private void LayoutGroupedBatchItems(float lineStep)
    {
        int count = Mathf.Min(visibleTextCount, textItems.Count);
        if (count <= 0)
            return;

        float horizontalStep = Mathf.Max(batchHorizontalStep, lineStep * 0.8f);
        var groupOrder = new List<int>(count);
        var columnByItem = new int[count];

        for (int i = 0; i < count; i++)
        {
            int groupKey = i < batchGroupKeys.Count ? batchGroupKeys[i] : int.MinValue + i;
            int column = groupOrder.IndexOf(groupKey);
            if (column < 0)
            {
                column = groupOrder.Count;
                groupOrder.Add(groupKey);
            }

            columnByItem[i] = column;
        }

        int columnCount = Mathf.Max(1, groupOrder.Count);
        float center = (columnCount - 1) * 0.5f;
        var rowByColumn = new int[columnCount];

        for (int i = 0; i < count; i++)
        {
            int column = columnByItem[i];
            int row = rowByColumn[column]++;
            float x = (column - center) * horizontalStep;
            float y = row * lineStep;
            textItems[i].rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }

    private void EnsureTextItemCount(int requiredCount)
    {
        if (text == null)
            return;

        if (textItems.Count == 0)
            textItems.Add(text);

        while (textItems.Count < requiredCount)
        {
            var clonedText = Instantiate(text, text.transform.parent);
            clonedText.name = $"{text.name}_{textItems.Count}";
            clonedText.raycastTarget = false;
            clonedText.enabled = false;
            clonedText.gameObject.SetActive(false);
            textItems.Add(clonedText);
        }
    }

    private void ApplyTextValue(TextMeshProUGUI item, string value)
    {
        if (item == null)
            return;

        item.gameObject.SetActive(true);
        item.enabled = true;
        item.SetText(value);
    }

    private void DeactivateTextItemsFrom(int startIndex)
    {
        for (int i = startIndex; i < textItems.Count; i++)
        {
            var item = textItems[i];
            if (item == null)
                continue;

            item.SetText(string.Empty);
            item.enabled = false;
            item.rectTransform.anchoredPosition = Vector2.zero;
            if (i > 0)
                item.gameObject.SetActive(false);
        }
    }

    private void ResetTextItems()
    {
        DeactivateTextItemsFrom(0);
        if (text != null)
            text.gameObject.SetActive(true);
    }

    #region IPoolObject

    public GameObject Origin { get; set; }
    public void OnCreateFromPool() {}
    public void OnGetFromPool() {}

    public void OnReleaseFromPool()
    {
        CancelAnimationTask();

        worldAnchor = null;
        worldOffset = Vector3.zero;
        worldPosition = Vector3.zero;
        visibleTextCount = 1;
        currentBatchLayout = FloatingTextBatchLayout.Spread;
        useGroupedBatchLayout = false;
        useBatchTimingStagger = false;
        batchGroupKeys.Clear();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        ResetTextItems();
        if (text != null)
        {
            text.enabled = false;
            text.rectTransform.anchoredPosition = Vector2.zero;
            if (animationData != null)
                text.color = animationData.TextColor;
        }
    }

    public void OnDestroyFromPool()
    {
        CancelAnimationTask();
    }

    public void ReleaseSelf()
    {
        if (Util.IsQuitting) return;
        if (!gameObject.activeSelf) return;

        PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion

    private void CancelAnimationTask()
    {
        if (_animCts == null) return;

        try
        {
            if (!_animCts.IsCancellationRequested)
                _animCts.Cancel();
        }
        catch (Exception)
        {
            Logg.LogWarning("invalid animation CTS");
        }
        finally
        {
            _animCts.Dispose();
            _animCts = null;
        }
    }

    private void StartFloatAndFadeTask()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
        FloatAndFadeAsync(_animCts.Token).Forget();
    }

    private async UniTask FloatAndFadeAsync(CancellationToken token)
    {
        float t = 0f;

        float fDuration = AnimationData.FadeOutDuration;
        float lTime = AnimationData.LifeTime;
        float fadeStart = Mathf.Max(0f, lTime - fDuration);
        float staggerDelay = useBatchTimingStagger ? Mathf.Max(0f, batchItemStaggerDelay) : 0f;
        float totalLife = lTime + (visibleTextCount > 1 ? staggerDelay * (visibleTextCount - 1) : 0f);

        while (t < totalLife)
        {
            if (token.IsCancellationRequested)
                break;

            float dt = Time.unscaledDeltaTime;
            t = Mathf.Min(t + dt, totalLife);

            worldOffset += Vector3.up * (AnimationData.RiseSpeed * dt);
            bool isVisibleOnScreen = UpdateScreenPosition();

            if (visibleTextCount <= 0)
                break;

            if (useBatchTimingStagger)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                if (isVisibleOnScreen)
                    ApplyStaggeredItemVisuals(t, lTime, fadeStart, fDuration, staggerDelay);
            }
            else if (t >= fadeStart && fDuration > 0f)
            {
                float u = Mathf.InverseLerp(fadeStart, lTime, t);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, u);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        ReleaseSelf();
    }

    private void ResolveCanvasContext()
    {
        rootCanvas = text != null ? text.GetComponentInParent<Canvas>() : null;
        rootCanvasRect = rootCanvas != null ? rootCanvas.rootCanvas.transform as RectTransform : null;
    }

    private bool UpdateScreenPosition()
    {
        if (text == null)
            return false;

        if (worldAnchor != null)
            worldPosition = worldAnchor.position;

        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null)
            worldCamera = FindFirstObjectByType<Camera>();
        if (worldCamera == null)
        {
            SetVisibleTexts(false);
            return false;
        }

        Vector3 targetWorldPos = worldPosition + worldOffset;
        Vector3 screenPos = worldCamera.WorldToScreenPoint(targetWorldPos);
        if (screenPos.z <= 0f)
        {
            SetVisibleTexts(false);
            return false;
        }

        var targetTransform = rootRect != null ? (Transform)rootRect : transform;
        if (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            targetTransform.position = screenPos;
        }
        else if (rootCanvasRect != null &&
                 RectTransformUtility.ScreenPointToWorldPointInRectangle(
                     rootCanvasRect, screenPos, GetCanvasCamera(), out var uiWorld))
        {
            targetTransform.position = uiWorld;
        }
        else
        {
            targetTransform.position = screenPos;
        }

        SetVisibleTexts(true);
        return true;
    }

    private void SetVisibleTexts(bool isVisible)
    {
        int count = Mathf.Min(visibleTextCount, textItems.Count);
        for (int i = 0; i < count; i++)
        {
            var item = textItems[i];
            if (item == null)
                continue;

            if (isVisible)
                item.gameObject.SetActive(true);

            if (!isVisible)
            {
                item.enabled = false;
                continue;
            }

            if (!useBatchTimingStagger)
                item.enabled = true;
        }
    }

    private void ApplyStaggeredItemVisuals(float elapsed, float itemLife, float fadeStart, float fadeDuration, float staggerDelay)
    {
        if (AnimationData == null)
            return;

        int count = Mathf.Min(visibleTextCount, textItems.Count);
        Color baseColor = AnimationData.TextColor;

        for (int i = 0; i < count; i++)
        {
            var item = textItems[i];
            if (item == null)
                continue;

            float itemElapsed = elapsed - (staggerDelay * i);
            bool isAlive = itemElapsed >= 0f && itemElapsed < itemLife;
            if (!isAlive)
            {
                item.enabled = false;
                continue;
            }

            float alpha = 1f;
            if (fadeDuration > 0f && itemElapsed >= fadeStart)
            {
                float u = Mathf.InverseLerp(fadeStart, itemLife, itemElapsed);
                alpha = Mathf.Lerp(1f, 0f, u);
            }

            var tint = baseColor;
            tint.a *= alpha;
            item.color = tint;
            item.enabled = true;
            item.gameObject.SetActive(true);
        }
    }

    private Camera GetCanvasCamera()
    {
        if (rootCanvas == null) return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
}

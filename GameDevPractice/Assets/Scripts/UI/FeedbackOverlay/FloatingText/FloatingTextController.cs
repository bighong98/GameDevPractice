using System;
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
    [SerializeField] private TMP_Text text;
    [Header("Animation")]
    [SerializeField] private FloatingTextSO animationData;

    [Header("Canvas Tuning")]
    [SerializeField] private float canvasFontScale = 12f;
    [SerializeField] private float minCanvasFontSize = 18f;

    private Transform textTrs;
    private Canvas rootCanvas;
    private RectTransform rootCanvasRect;
    private Transform worldAnchor;
    private Vector3 worldPosition;
    private Vector3 worldOffset;
    private IFloatingTextData AnimationData => animationData;

    private CancellationTokenSource _animCts;
    private Camera worldCamera;

    private void Awake()
    {
        text = ResolveText();
        if (text == null)
        {
            Logg.LogError($"[{nameof(FloatingTextController)}] failed to resolve text component");
            enabled = false;
            return;
        }

        textTrs = text.transform;
        ResolveCanvasContext();
    }

    private void OnEnable()
    {
        worldCamera = Camera.main;
        ResolveCanvasContext();
    }

    #region IFloatingTextController

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

        text.SetText(s);
        Refresh();
    }

    #endregion

    public void SetWorldAnchor(Transform anchor)
    {
        worldAnchor = anchor;
        if (worldAnchor != null)
            worldPosition = worldAnchor.position;
    }

    public void SetWorldPosition(Vector3 position)
    {
        worldAnchor = null;
        worldPosition = position;
    }

    private void Refresh()
    {
        if (text == null) return;

        ApplySetting();
        text.enabled = true;
        text.havePropertiesChanged = true;
        text.ForceMeshUpdate();
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

        CancelAnimationTask();
        StartFloatAndFadeTask();
    }

    private void ApplySetting()
    {
        worldOffset = AnimationData.StartOffset;
        text.color = AnimationData.TextColor;
        text.fontSize = ResolveFontSize(AnimationData.TextSize);
        UpdateScreenPosition();
    }

    private float ResolveFontSize(float configuredSize)
    {
        if (text is TextMeshProUGUI)
            return Mathf.Max(minCanvasFontSize, configuredSize * canvasFontScale);

        return configuredSize;
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
        if (text)
        {
            text.enabled = false;
            text.color = AnimationData.TextColor;
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

        Color c = text.color;

        while (t < lTime)
        {
            if (token.IsCancellationRequested)
                break;

            float dt = Time.unscaledDeltaTime;
            t = Mathf.Min(t + dt, lTime);

            worldOffset += Vector3.up * (AnimationData.RiseSpeed * dt);
            UpdateScreenPosition();

            if (text == null)
                break;

            if (t >= fadeStart && fDuration > 0f)
            {
                float u = Mathf.InverseLerp(fadeStart, lTime, t);
                c.a = Mathf.Lerp(1f, 0f, u);
                text.color = c;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        ReleaseSelf();
    }

    private TMP_Text ResolveText()
    {
        if (text != null)
            return EnsureCanvasText(text);

        if (Util.FindChild<TextMeshProUGUI>(gameObject, "text", true) is { } uiText)
            return uiText;

        if (Util.FindChild<TextMeshPro>(gameObject, "text", true) is { } worldText)
            return ConvertLegacyText(worldText);

        return null;
    }

    private TMP_Text EnsureCanvasText(TMP_Text tmp)
    {
        if (tmp is TextMeshProUGUI)
            return tmp;
        if (tmp is TextMeshPro worldText)
            return ConvertLegacyText(worldText);
        return tmp;
    }

    private TextMeshProUGUI ConvertLegacyText(TextMeshPro legacy)
    {
        if (legacy == null) return null;

        var go = legacy.gameObject;
        var ugui = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        ugui.font = legacy.font;
        ugui.fontSize = legacy.fontSize;
        ugui.color = legacy.color;
        ugui.alignment = legacy.alignment;
        ugui.text = legacy.text;
        ugui.raycastTarget = false;

        legacy.enabled = false;
        if (go.TryGetComponent<MeshRenderer>(out var renderer))
            renderer.enabled = false;

        return ugui;
    }

    private void ResolveCanvasContext()
    {
        rootCanvas = text != null ? text.GetComponentInParent<Canvas>() : null;
        rootCanvasRect = rootCanvas != null ? rootCanvas.rootCanvas.transform as RectTransform : null;
    }

    private void UpdateScreenPosition()
    {
        if (textTrs == null || text == null) return;

        if (worldAnchor != null)
            worldPosition = worldAnchor.position;

        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null)
            worldCamera = FindFirstObjectByType<Camera>();
        if (worldCamera == null)
        {
            text.enabled = false;
            return;
        }

        Vector3 targetWorldPos = worldPosition + worldOffset;
        Vector3 screenPos = worldCamera.WorldToScreenPoint(targetWorldPos);
        if (screenPos.z <= 0f)
        {
            text.enabled = false;
            return;
        }

        if (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            textTrs.position = screenPos;
        }
        else if (rootCanvasRect != null &&
                 RectTransformUtility.ScreenPointToWorldPointInRectangle(
                     rootCanvasRect, screenPos, GetCanvasCamera(), out var uiWorld))
        {
            textTrs.position = uiWorld;
        }
        else
        {
            textTrs.position = screenPos;
        }

        if (!text.enabled)
            text.enabled = true;
    }

    private Camera GetCanvasCamera()
    {
        if (rootCanvas == null) return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
}

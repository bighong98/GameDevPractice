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

// 풀링된 플로팅 텍스트 UI 인스턴스 1개를 생명주기/레이아웃/애니메이션 관점에서 제어하는 컨트롤러
public class FloatingTextController : MonoBehaviour, IPoolObject, IFloatingTextController
{
    // 기본 텍스트 표시용 TMP 컴포넌트
    [SerializeField] private TextMeshProUGUI text;
    [Header("Animation")]
    // 상승/페이드/색상/기본 폰트 데이터 SO
    [SerializeField] private FloatingTextSO animationData;

    [Header("Canvas Tuning")]
    // SO 폰트 크기 -> 캔버스 폰트 크기 변환 배율
    [SerializeField] private float canvasFontScale = 12f;
    // 캔버스 기준 최소 폰트 크기 하한
    [SerializeField] private float minCanvasFontSize = 18f;
    
    [Header("Batch Layout")]
    // 라인 배치 시 행 간격 계수
    [SerializeField] private float batchLineHeightMultiplier = 0.9f;
    // 스프레드/그룹 배치 시 열 간격 기본값
    [SerializeField] private float batchHorizontalStep = 18f;
    // 배치에서 실제 렌더링 허용 최대 항목 수
    [SerializeField] private int maxBatchEntries = 6;
    // 배치 타이밍 스태거 지연 간격 sec
    [SerializeField] private float batchItemStaggerDelay = 0.04f;

    // 활성/비활성 포함 전체 텍스트 아이템 풀 목록 인스턴스 내부용
    private readonly List<TextMeshProUGUI> textItems = new();
    // 루트 RectTransform 캐시
    private RectTransform rootRect;
    // 부모 Canvas 캐시
    private Canvas rootCanvas;
    // 루트 Canvas RectTransform 캐시
    private RectTransform rootCanvasRect;
    // 월드 앵커 Transform 추적 대상
    private Transform worldAnchor;
    // 앵커 기준 현재 월드 위치 캐시
    private Vector3 worldPosition;
    // 애니메이션 상승 오프셋 누적값
    private Vector3 worldOffset;
    // 내부 설정 데이터 공통 접근 프로퍼티
    private IFloatingTextData AnimationData => animationData;

    // 플로팅 애니메이션 취소 제어 토큰 소스
    private CancellationTokenSource _animCts;
    // 월드 -> 스크린 좌표 변환용 카메라 캐시
    private Camera worldCamera;
    // 전체 알파 제어용 CanvasGroup
    private CanvasGroup canvasGroup;
    // 현재 배치 레이아웃 모드
    private FloatingTextBatchLayout currentBatchLayout = FloatingTextBatchLayout.Spread;
    // 배치 항목별 그룹 키 목록 같은 키는 같은 열 배치 용도
    private readonly List<int> batchGroupKeys = new();
    // 그룹 기반 배치 사용 여부
    private bool useGroupedBatchLayout;
    // 항목별 시간차 노출/페이드 사용 여부
    private bool useBatchTimingStagger;
    // 현재 표시 활성 항목 수
    private int visibleTextCount = 1;

    // 컴포넌트 참조 확보 + 아이템 리스트 초기화 + 캔버스 문맥 캐싱
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

    // 활성화 시 카메라/캔버스 문맥 최신화
    private void OnEnable()
    {
        worldCamera = Camera.main;
        ResolveCanvasContext();
    }

    // 설정 + 텍스트 동시 적용 편의 메서드
    public void Set(FloatingTextSO data, string textValue)
    {
        SetSetting(data);
        SetText(textValue);
    }

    // 설정 데이터 교체
    public void SetSetting(FloatingTextSO data)
    {
        animationData = data;
    }

    // 단일 텍스트 모드 설정 배치 상태 초기화 포함
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

    // 배치 텍스트 기본 레이아웃 오버로드 Spread
    public void SetBatchTexts(IReadOnlyList<string> values)
    {
        SetBatchTexts(values, FloatingTextBatchLayout.Spread);
    }

    // 문자열 목록 배치 설정 레이아웃 지정 버전
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

    // 문자열 + 공격 인스턴스 ID 목록 배치 설정 그룹 키 기반 열 배치 버전
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


    // 월드 앵커 지정 현재 앵커 좌표 동기화 포함
    public void SetWorldAnchor(Transform anchor)
    {
        worldAnchor = anchor;
        if (worldAnchor != null)
            worldPosition = worldAnchor.position;
    }
    
    // 월드 앵커 지정 실패 대비 폴백 월드 좌표 동시 지정 버전
    public void SetWorldAnchor(Transform anchor, Vector3 fallbackWorldPosition)
    {
        worldAnchor = anchor;
        if (worldAnchor != null)
            worldPosition = worldAnchor.position;
        else
            worldPosition = fallbackWorldPosition;
    }

    // 현재 데이터 기준 시각 속성 반영 + 메시 갱신 + 애니메이션 재시작
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

    // 텍스트 색상/크기/레이아웃 및 초기 가시 상태 적용
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

    // SO 텍스트 크기를 캔버스 표시 크기로 변환 후 하한 적용
    private float ResolveFontSize(float configuredSize)
    {
        return Mathf.Max(minCanvasFontSize, configuredSize * canvasFontScale);
    }
    
    // 현재 배치 모드에 따라 visible 항목 anchoredPosition 배치 계산
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

    // 그룹 키 기준 열 분리 + 열 내 행 적층 배치 로직
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

    // 필요한 항목 수까지 TMP 텍스트 인스턴스 생성 보장
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

    // 단일 아이템 텍스트/활성 상태 적용 헬퍼
    private void ApplyTextValue(TextMeshProUGUI item, string value)
    {
        if (item == null)
            return;

        item.gameObject.SetActive(true);
        item.enabled = true;
        item.SetText(value);
    }

    // startIndex 이후 항목 비활성화 + 위치/텍스트 초기화
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

    // 텍스트 아이템 전체 초기화 기본 텍스트 1개만 활성 유지
    private void ResetTextItems()
    {
        DeactivateTextItemsFrom(0);
        if (text != null)
            text.gameObject.SetActive(true);
    }

    #region IPoolObject

    // 풀 원본 프리팹 참조 프로퍼티
    public GameObject Origin { get; set; }
    // 풀 생성 시 콜백 현재 구현 비움
    public void OnCreateFromPool() {}
    // 풀 대여 시 콜백 현재 구현 비움
    public void OnGetFromPool() {}

    // 풀 반납 시 런타임 상태/가시 상태/배치 상태 초기화
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

    // 풀 파기 시 애니메이션 토큰 정리
    public void OnDestroyFromPool()
    {
        CancelAnimationTask();
    }

    // 자기 자신 풀 반납 요청 앱 종료/비활성 상태 가드 포함
    public void ReleaseSelf()
    {
        if (Util.IsQuitting) return;
        if (!gameObject.activeSelf) return;

        PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion

    // 기존 애니메이션 취소 토큰 취소 + dispose 정리
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

    // 현재 오브젝트 수명과 연결된 애니메이션 작업 시작
    private void StartFloatAndFadeTask()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
        FloatAndFadeAsync(_animCts.Token).Forget();
    }

    // 상승 이동 + 화면 위치 갱신 + 페이드 진행 비동기 루프
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

    // 현재 텍스트 부모 기준 캔버스 문맥 참조 갱신
    private void ResolveCanvasContext()
    {
        rootCanvas = text != null ? text.GetComponentInParent<Canvas>() : null;
        rootCanvasRect = rootCanvas != null ? rootCanvas.rootCanvas.transform as RectTransform : null;
    }

    // 월드 좌표를 캔버스 좌표로 투영하고 가시 상태 적용 성공 여부 반환
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

    // visibleTextCount 범위 항목 활성/비활성 제어
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

    // 항목별 시간차 등장/시간차 페이드 계산 후 색상 알파 반영
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

    // 캔버스 렌더 모드에 맞는 좌표 변환용 카메라 반환
    private Camera GetCanvasCamera()
    {
        if (rootCanvas == null) return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
}

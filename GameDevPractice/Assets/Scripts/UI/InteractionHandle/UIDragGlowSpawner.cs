using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;

namespace TH.UI
{
    public sealed class UIDragGlowSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject pointPrefab;
        [Tooltip("RectTransformUtility 사용에 필요한 부모 캔버스 RectTransform. 런타임에 canvasType과 동일한 캔버스 컨테이너의 참조가 할당되어야 함")]
        [SerializeField] private RectTransform targetRect;

        [Header("Canvas Sorting")]
        [SerializeField] private UICanvas canvasType = UICanvas.FeedbackOverlay;

        [Header("Settings")]
        // 기본 크기
        [SerializeField] private float pointSize = 120f;
        // 펄스 주기
        [SerializeField] private float blinkInterval = 0.35f;
        // 크기 변화 비율
        [SerializeField] private float pulseSizeScale = 0.08f;
        // 최소 밝기 비율
        [SerializeField] private float pulseMinIntensity = 0.4f;

        private IRaycastHandler raycastHandler;
        private bool isInitialized;
        private bool isDragging;

        private TouchGlowUIPoint activePoint;
        private CancellationTokenSource dragCts;
        private float pulseStartTime;

        private void Awake()
        {
            if (pointPrefab == null)
            {
                this.LogWarning("pointPrefab is invalid", context: this);
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            raycastHandler = ServiceLocator.Get<IRaycastHandler>();
        }

        private void OnEnable()
        {
            // 드래그 이벤트 구독
            InputManager.Instance.OnScreenDragStarted += HandleDragStarted;
            InputManager.Instance.OnUIDragStarted += HandleDragStarted;
            InputManager.Instance.OnScreenDragEnded += HandleDragEnded;
            InputManager.Instance.OnUIDragEnded += HandleDragEnded;
            InputManager.Instance.OnScreenDragging += HandlePointerMoved;
            InputManager.Instance.OnUIDragging += HandlePointerMoved;
        }

        private void OnDisable()
        {
            // 드래그 이벤트 해제 및 정리
            InputManager.Instance.OnScreenDragStarted -= HandleDragStarted;
            InputManager.Instance.OnUIDragStarted -= HandleDragStarted;
            InputManager.Instance.OnScreenDragEnded -= HandleDragEnded;
            InputManager.Instance.OnUIDragEnded -= HandleDragEnded;
            InputManager.Instance.OnScreenDragging -= HandlePointerMoved;
            InputManager.Instance.OnUIDragging -= HandlePointerMoved;

            CancelDragTask();
            ReleaseActivePoint();
        }

        private void HandleDragStarted(Vector2 screenPos)
        {
            // 드래그 시작 처리
            if (isDragging) return;
            // 스크린 좌표 -> 로컬 좌표 변환
            if (!EnsureTargetRect()) return;
            if (!raycastHandler.GetMouseScreenPosition(targetRect, screenPos, out var localPos)) return;

            isDragging = true;

            // 풀 오브젝트 확보
            if (activePoint.IsNull())
                activePoint = UIManager.Instance.GetUIFromPool<TouchGlowUIPoint>(pointPrefab, canvasType);

            if (activePoint.IsNull())
                return;

            // 펄스 루프 시작
            StartDragTask();
        }

        private void HandlePointerMoved(Vector2 screenPos)
        {
            // 드래그 중 위치 갱신
            if (!isDragging) return;
            if (!EnsureTargetRect()) return;
            if (!raycastHandler.GetMouseScreenPosition(targetRect, screenPos, out var localPos)) return;

            if (activePoint.IsNotNull())
                activePoint.SetPosition(localPos);
        }

        private void HandleDragEnded(Vector2 screenPos)
        {
            // 드래그 종료 정리
            if (!isDragging)
                return;

            isDragging = false;
            CancelDragTask();
            ReleaseActivePoint();
        }

        private void StartDragTask()
        {
            // 펄스 루프 시작
            float now = Time.unscaledTime;
            pulseStartTime = now;
            
            CancelDragTask();
            dragCts = new CancellationTokenSource();
            RunDragGlowAsync(dragCts.Token).Forget();
        }

        private async UniTaskVoid RunDragGlowAsync(CancellationToken token)
        {
            // 선형 펄스 루프
            if (activePoint == null)
                return;

            float period = Mathf.Max(0.05f, blinkInterval);
            float sizeScale = Mathf.Clamp(pulseSizeScale, 0f, 0.5f);
            float minIntensity = Mathf.Clamp01(pulseMinIntensity);
            while (!token.IsCancellationRequested)
            {
                if (!isDragging || activePoint == null)
                    return;

                float now = Time.unscaledTime;
                // 셰이더 타임 래핑
                activePoint.SetTimeNowWrapped(now);

                // 주기 내 정규화 위상 계산
                float phase = Mathf.Repeat((now - pulseStartTime) / period, 1f);
                // 삼각파 형태 보간 값
                float t = phase <= 0.5f ? phase * 2f : (1f - phase) * 2f;
                // 크기/밝기 선형 보간
                float size = pointSize * Mathf.Lerp(1f - sizeScale, 1f + sizeScale, t);
                float intensity = Mathf.Lerp(minIntensity, 1f, t);
                activePoint.SetSize(size);
                activePoint.SetIntensity(intensity);

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

        private bool EnsureTargetRect()
        {
            // 캔버스 RectTransform 확보
            if (isInitialized) return true;
            if (!isInitialized && UIManager.Instance.GetCanvasRect(canvasType) is { } resultRect && resultRect.IsNotNull())
            {
                targetRect = resultRect;
                isInitialized = true;
                return true;
            }

            // UIManager로부터 캔버스를 받아오지 못한 경우 fallback으로 현재 오브젝트의 캔버스를 받아옴
            this.LogWarning("EnsureTargetRect() - failed to get canvas from UIManager. fallback with temporary canvas", context: this);
            var canvas = GetComponentInParent<Canvas>();
            targetRect = canvas != null ? canvas.transform as RectTransform : null;
            isInitialized = targetRect != null;

            return isInitialized;
        }

        private void CancelDragTask()
        {
            // 루프 취소 처리
            if (dragCts == null)
                return;

            try
            {
                if (!dragCts.IsCancellationRequested)
                    dragCts.Cancel();
            }
            finally
            {
                dragCts.Dispose();
                dragCts = null;
            }
        }

        private void ReleaseActivePoint()
        {
            // 풀 반환 처리
            if (activePoint.IsNull())
                return;

            UIManager.Instance.ReleaseUI(activePoint);
            activePoint = null;
        }
    }
}

using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;

namespace TH.UI
{
    public sealed class UITouchGlowSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject pointPrefab;
        [Tooltip("RectTransformUtility 사용에 필요한 부모 캔버스 RectTransform. 런타임에 canvasType과 동일한 캔버스 컨테이너의 참조가 할당되어야 함")]
        [SerializeField] private RectTransform targetRect;

        [Header("Canvas Sorting")]
        [SerializeField] private UICanvas canvasType = UICanvas.FeedbackOverlay;

        [Header("Settings")]
        [SerializeField] private float glowLifetime = 1.0f;
        [SerializeField] private float pointSize = 120f;

        private IRaycastHandler raycastHandler;
        private bool isInitialized;
        private CancellationTokenSource glowCts;

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
            glowCts = new CancellationTokenSource();
            InputManager.Instance.OnPointerPressed += HandlePointerPressed;
        }

        private void OnDisable()
        {
            InputManager.Instance.OnPointerPressed -= HandlePointerPressed;
            CancelGlowTasks();
        }

        private void HandlePointerPressed(Vector2 screenPos)
        {
            if (!EnsureTargetRect()) return;
            if (!raycastHandler.GetMouseScreenPosition(targetRect, screenPos, out var localPos)) return;

            var point = UIManager.Instance.GetUIFromPool<TouchGlowUIPoint>(pointPrefab, canvasType);
            if (point.IsNotNull())
            {
                float now = Time.unscaledTime;
                point.Play(localPos, now, glowLifetime, pointSize);
                TrackGlowAsync(point, now, glowLifetime, glowCts.Token).Forget();
            }
        }

        private async UniTaskVoid TrackGlowAsync(TouchGlowUIPoint point, float startTime, float lifetime, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (point == null)
                    return;

                float now = Time.unscaledTime;
                point.SetTimeNow(now);

                if (now - startTime > lifetime)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            if (point != null)
                point.ReleaseSelf();
        }

        private bool EnsureTargetRect()
        {
            if (isInitialized) return true;
            if (!isInitialized && UIManager.Instance.GetCanvasRect(canvasType) is {} resultRect && resultRect.IsNotNull())
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

        private void CancelGlowTasks()
        {
            if (glowCts == null)
                return;

            try
            {
                if (!glowCts.IsCancellationRequested)
                    glowCts.Cancel();
            }
            finally
            {
                glowCts.Dispose();
                glowCts = null;
            }
        }
    }
}



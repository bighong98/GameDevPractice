using TH.Core;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;

namespace TH.UI
{
    public sealed class TouchGlowUISpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject pointPrefab;
        [SerializeField] private RectTransform targetRect;

        [Header("Canvas Sorting")]
        [SerializeField] private UICanvas canvasType = UICanvas.Feedback;

        private IRaycastHandler raycastHandler;
        private bool isInitialized;

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
            EnsureTargetRect();
            raycastHandler = ServiceLocator.Get<IRaycastHandler>();
        }

        private void OnEnable()
        {
            InputManager.Instance.OnPointerPressed += HandlePointerPressed;
        }

        private void OnDisable()
        {
            InputManager.Instance.OnPointerPressed -= HandlePointerPressed;
        }

        private void HandlePointerPressed(Vector2 screenPos)
        {
            if (!EnsureTargetRect()) return;
            if (!raycastHandler.GetMouseScreenPosition(targetRect, screenPos, out var localPos)) return;

            var point = UIManager.Instance.GetUIFromPool<TouchGlowUIPoint>(pointPrefab, canvasType);
            if (point.IsNotNull())
                point.Play(localPos);
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
    }
}

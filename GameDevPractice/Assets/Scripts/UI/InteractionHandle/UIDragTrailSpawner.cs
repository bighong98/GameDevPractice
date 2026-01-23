using TH.Core;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;

namespace TH.UI
{
    public sealed class UIDragTrailSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject trailPrefab;

        [Header("Canvas Sorting")]
        [SerializeField] private UICanvas canvasType = UICanvas.FeedbackOverlay;

        private CanvasDragTrailUI activeTrail;
        
        private bool isDragging;

        private void Awake()
        {
            if (trailPrefab == null)
            {
                this.LogWarning("trailPrefab is invalid", context: this);
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            InputManager.Instance.OnScreenDragStarted += HandleDragStarted;
            InputManager.Instance.OnUIDragStarted += HandleDragStarted;
            InputManager.Instance.OnScreenDragEnded += HandleDragEnded;
            InputManager.Instance.OnUIDragEnded += HandleDragEnded;
            InputManager.Instance.OnScreenDragging += HandlePointerMoved;
            InputManager.Instance.OnUIDragging += HandlePointerMoved;
        }

        private void OnDisable()
        {
            InputManager.Instance.OnScreenDragStarted -= HandleDragStarted;
            InputManager.Instance.OnUIDragStarted -= HandleDragStarted;
            InputManager.Instance.OnScreenDragEnded -= HandleDragEnded;
            InputManager.Instance.OnUIDragEnded -= HandleDragEnded;
            InputManager.Instance.OnScreenDragging -= HandlePointerMoved;
            InputManager.Instance.OnUIDragging -= HandlePointerMoved;

            ReleaseActiveTrail();
        }

        private void HandleDragStarted(Vector2 screenPos)
        {
            if (isDragging) return;
            isDragging = true;

            activeTrail = UIManager.Instance.GetUIFromPool<CanvasDragTrailUI>(trailPrefab, canvasType);
            if (activeTrail.IsNotNull())
                activeTrail.AddPoint(screenPos);
        }

        private void HandlePointerMoved(Vector2 screenPos)
        {
            if (!isDragging || activeTrail.IsNull())
                return;

            activeTrail.AddPoint(screenPos);
        }

        private void HandleDragEnded(Vector2 screenPos)
        {
            if (!isDragging)
                return;

            if (activeTrail.IsNotNull())
                activeTrail.AddPoint(screenPos);

            isDragging = false;
            ReleaseActiveTrail();
        }

        private void ReleaseActiveTrail()
        {
            if (activeTrail.IsNull())
                return;

            activeTrail.ClearTrail();
            UIManager.Instance.ReleaseUI(activeTrail);
            activeTrail = null;
        }
    }
}

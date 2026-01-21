using TH.Core.Pool;
using TH.Core.Service;
using UnityEngine;

namespace TH.UI
{
    [RequireComponent(typeof(CanvasLineRenderer))]
    public sealed class CanvasDragTrailUI : BaseUI, IPoolObject
    {
        [Header("References")]
        [SerializeField] private CanvasLineRenderer dragTrail;

        public GameObject Origin { get; set; }

        protected override void Awake()
        {
            base.Awake();
            
            if (dragTrail == null)
                dragTrail = gameObject.GetOrAddComponent<CanvasLineRenderer>();
            dragTrail.SetCamera(null); // Overlay 모드에서 사용 의도
        }

        public void AddPoint(Vector2 screenPosition)
        {
            if (dragTrail == null)
                return;

            dragTrail.AddPoint(screenPosition);
        }

        public void ClearTrail()
        {
            if (dragTrail == null)
                return;

            dragTrail.Clear();
        }

        public void OnCreateFromPool()
        {
            
        }

        public void OnGetFromPool()
        {
            ClearTrail();
        }

        public void OnReleaseFromPool()
        {
            ClearTrail();
        }

        public void OnDestroyFromPool()
        {
        }

        public void ReleaseSelf()
        {
            if (Util.IsQuitting || !gameObject.activeSelf)
                return;

            UIManager.Instance.ReleaseUI(this);
        }
    }
}

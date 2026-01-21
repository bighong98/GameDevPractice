using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public sealed class TouchGlowUIPoint : BaseUI, IPoolObject
    {
        [Header("References")]
        [SerializeField] private Material glowMaterial;

        [Header("Settings")]
        [SerializeField] private float glowLifetime = 1.0f;
        [SerializeField] private float pointSize = 120f;

        private RectTransform rectTransform;
        private RawImage rawImage;
        private Material runtimeMaterial;
        private CancellationTokenSource lifetimeCts;

        private static readonly int StartTimeID = Shader.PropertyToID("_StartTime");
        private static readonly int TimeNowID = Shader.PropertyToID("_TimeNow");
        private static readonly int LifetimeID = Shader.PropertyToID("_Lifetime");

        public GameObject Origin { get; set; }

        protected override void Awake()
        {
            base.Awake();
            CacheComponents();
        }

        public void Play(Vector2 anchoredPosition)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchoredPosition = anchoredPosition;
            StartGlow();
        }

        public void OnCreateFromPool()
        {
            CacheComponents();
            EnsureMaterial();
            ApplySize();
        }

        public void OnGetFromPool()
        {
            ApplySize();
        }

        public void OnReleaseFromPool()
        {
            CancelLifetimeTask();
        }

        public void OnDestroyFromPool()
        {
            CancelLifetimeTask();
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        public void ReleaseSelf()
        {
            if (Util.IsQuitting || !gameObject.activeSelf)
                return;

            UIManager.Instance.ReleaseUI(this);
        }

        private void CacheComponents()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rawImage == null)
                rawImage = GetComponent<RawImage>();
        }

        private void ApplySize()
        {
            if (rectTransform == null)
                return;

            rectTransform.sizeDelta = new Vector2(pointSize, pointSize);
        }

        private void EnsureMaterial()
        {
            if (rawImage == null || runtimeMaterial != null)
                return;

            var source = glowMaterial != null ? glowMaterial : rawImage.material;
            if (source == null)
                return;

            runtimeMaterial = new Material(source);
            rawImage.material = runtimeMaterial;
        }

        private void StartGlow()
        {
            EnsureMaterial();
            if (runtimeMaterial == null)
                return;

            float startTime = Time.unscaledTime;
            runtimeMaterial.SetFloat(LifetimeID, glowLifetime);
            runtimeMaterial.SetFloat(StartTimeID, startTime);
            runtimeMaterial.SetFloat(TimeNowID, startTime);

            CancelLifetimeTask();
            var destroyToken = this.GetCancellationTokenOnDestroy();
            lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            TrackLifetimeAsync(startTime, lifetimeCts.Token).Forget();
        }

        private void CancelLifetimeTask()
        {
            if (lifetimeCts == null)
                return;

            try
            {
                if (!lifetimeCts.IsCancellationRequested)
                    lifetimeCts.Cancel();
            }
            catch (Exception)
            {
                Logg.LogWarning("[TouchGlowUIPoint] invalid lifetime CTS");
            }
            finally
            {
                lifetimeCts.Dispose();
                lifetimeCts = null;
            }
        }

        private async UniTaskVoid TrackLifetimeAsync(float startTime, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float now = Time.unscaledTime;
                runtimeMaterial.SetFloat(TimeNowID, now);

                if (now - startTime > glowLifetime)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            ReleaseSelf();
        }
    }
}

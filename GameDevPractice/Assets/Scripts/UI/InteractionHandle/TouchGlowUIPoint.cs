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

        // 컴포넌트 캐시
        private RectTransform rectTransform;
        private RawImage rawImage;
        // 런타임 머티리얼 인스턴스
        private Material runtimeMaterial;
        // 재생 파라미터
        private float glowLifetime;
        private float pointSize;
        private float startTime;
        // 재생 상태
        private bool isPlaying;
        // 기본 색상 캐시
        private Color baseColor;
        private bool hasBaseColor;

        private static readonly int StartTimeID = Shader.PropertyToID("_StartTime");
        private static readonly int TimeNowID = Shader.PropertyToID("_TimeNow");
        private static readonly int LifetimeID = Shader.PropertyToID("_Lifetime");

        // 풀 원본 프리팹
        public GameObject Origin { get; set; }

        protected override void Awake()
        {
            base.Awake();
            // 초기 캐시 확보
            CacheComponents();
        }

        public void Play(Vector2 anchoredPosition, float startTime, float lifetime, float size)
        {
            // 포인터 위치 및 재생 파라미터 반영
            SetPosition(anchoredPosition);
            SetVisible(true);
            pointSize = size;
            glowLifetime = lifetime;

            // 크기 적용 및 셰이더 타임 초기화
            ApplySize();
            StartGlow(startTime);
        }

        public void SetPosition(Vector2 anchoredPosition)
        {
            // 앵커 좌표 갱신
            if (rectTransform == null)
                return;

            rectTransform.anchoredPosition = anchoredPosition;
        }


        public void SetTimeNow(float now)
        {
            // 셰이더 타임 갱신
            if (runtimeMaterial == null)
                return;

            runtimeMaterial.SetFloat(TimeNowID, now);
        }

        public void SetTimeNowWrapped(float now)
        {
            // 수명 범위 내에서 래핑된 타임 갱신
            if (runtimeMaterial == null || !isPlaying)
                return;

            // 수명 미사용 처리
            if (glowLifetime <= 0f)
            {
                runtimeMaterial.SetFloat(TimeNowID, now);
                return;
            }

            // 시작 기준 래핑 값 계산
            float wrapped = startTime + Mathf.Repeat(now - startTime, glowLifetime);
            runtimeMaterial.SetFloat(TimeNowID, wrapped);
        }

        public void SetIntensity(float intensity)
        {
            // 기본 색상 기준 알파 보간
            if (rawImage == null)
                return;

            if (!hasBaseColor)
            {
                // 최초 색상 기준 저장
                baseColor = rawImage.color;
                hasBaseColor = true;
            }

            // 알파 스케일 반영
            float clamped = Mathf.Clamp01(intensity);
            rawImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * clamped);
        }

        public void SetSize(float size)
        {
            // 펄스 크기 반영
            pointSize = size;
            ApplySize();
        }

        public void SetVisible(bool isVisible)
        {
            // 이미지 가시성 토글
            if (rawImage == null)
                return;

            rawImage.enabled = isVisible;
        }

        public bool IsExpired(float now)
        {
            if (!isPlaying)
                return true;

            return now - startTime > glowLifetime;
        }

        public void OnCreateFromPool()
        {
            CacheComponents();
            EnsureMaterial();
        }

        public void OnGetFromPool()
        {
        }

        public void OnReleaseFromPool()
        {
            isPlaying = false;
        }

        public void OnDestroyFromPool()
        {
            isPlaying = false;
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
            // 필수 컴포넌트 캐시
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rawImage == null)
                rawImage = GetComponent<RawImage>();

            if (!hasBaseColor && rawImage != null)
            {
                // 초기 색상 캐시
                baseColor = rawImage.color;
                hasBaseColor = true;
            }
        }

        private void ApplySize()
        {
            // UI 크기 반영
            if (rectTransform == null)
                return;

            rectTransform.sizeDelta = new Vector2(pointSize, pointSize);
        }

        private void EnsureMaterial()
        {
            // 런타임 머티리얼 보장
            if (rawImage == null || runtimeMaterial != null)
                return;

            // 소스 머티리얼 결정
            var source = glowMaterial != null ? glowMaterial : rawImage.material;
            if (source == null)
                return;

            // 인스턴스 머티리얼 생성
            runtimeMaterial = new Material(source);
            rawImage.material = runtimeMaterial;
        }

        private void StartGlow(float newStartTime)
        {
            // 재생 상태 초기화
            EnsureMaterial();
            if (runtimeMaterial == null)
                return;

            startTime = newStartTime;
            isPlaying = true;

            // 셰이더 타임/수명 반영
            runtimeMaterial.SetFloat(LifetimeID, glowLifetime);
            runtimeMaterial.SetFloat(StartTimeID, startTime);
            runtimeMaterial.SetFloat(TimeNowID, startTime);
        }
    }
}

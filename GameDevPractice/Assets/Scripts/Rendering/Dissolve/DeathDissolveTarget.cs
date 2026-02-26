using LineworkLite.Common.Attributes;
using TH.Attribute;
using UnityEngine;
using UnityEngine.Serialization;

namespace TH.Rendering.Dissolve
{
    // 레거시 디졸브 컴포넌트 호환 어댑터
    // 신규 통합 컨트롤러 설정 전달 브리지
    [DisallowMultipleComponent]
    public sealed class DeathDissolveTarget : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private Health health;
        [SerializeField] private bool autoBindHealthOnAwake = true;

        [Header("Target")]
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Layer Routing")]
        [SerializeField] private bool changeObjectLayerOnStart = true;
        [SerializeField] private bool includeChildrenLayerChange = true;
        [SerializeField, TH.Editor.Layer] private int dissolveLayer = -1;
        [FormerlySerializedAs("dissolveLayerName")]
        [SerializeField, HideInInspector] private string legacyDissolveLayerName = "DissolveOnly";
        [SerializeField] [RenderingLayerMask] private uint dissolveRenderingLayerMask = 1u << 3;

        [Header("Timeline")]
        [SerializeField, Min(0f)] private float dissolveStartDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float dissolveDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float dissolveStartValue = 0f;
        [SerializeField, Range(0f, 1f)] private float dissolveEndValue = 1f;
        [SerializeField] private bool restoreStateOnDisable = true;

        [Header("Property Mapping")]
        [SerializeField] private bool remapMainTexToBaseMap = true;
        [SerializeField] private bool remapColorToBaseColor = true;
        [SerializeField] private bool remapBumpToNormal = true;
        [SerializeField] private bool forceOpaqueBaseColorAlpha = true;

        // 신규 통합 컨트롤러 캐시 참조
        private CharacterSpecialEffectController cachedController;

        public void ConfigureDefaults(string layerName, uint renderingLayerMask)
        {
            // 에디터 자동 세팅 기본값 반영 구간
            legacyDissolveLayerName = layerName;
            dissolveLayer = LayerMask.NameToLayer(layerName);
            dissolveRenderingLayerMask = renderingLayerMask;
            autoCollectRenderers = true;
            includeInactiveRenderers = true;
            changeObjectLayerOnStart = true;
            includeChildrenLayerChange = true;
            restoreStateOnDisable = true;

            ApplyToController();
        }

        private void Awake()
        {
            // 런타임 시작 시 컨트롤러 동기화 진입점
            ApplyToController();
        }

        private void OnValidate()
        {
            // 인스펙터 값 변경 즉시 동기화 구간
            if (!Application.isPlaying)
            {
                ApplyToController();
            }
        }

        [ContextMenu("Trigger Dissolve")]
        public void StartDissolve()
        {
            // 디버그 수동 실행 경로 설정 동기화 + 디졸브 시작
            ApplyToController();
            if (cachedController != null)
            {
                cachedController.StartDeathDissolve();
            }
        }

        private void ApplyToController()
        {
            // 레거시 문자열 레이어명 기반 인덱스 보정 구간
            SyncLayerIndexFromLegacyName();
            NormalizeDissolveRangeForward();
            // 신규 컨트롤러 획득 또는 생성 구간
            var controller = GetOrCreateController();
            if (controller == null)
            {
                return;
            }

            // 어댑터 직렬화 필드 일괄 전달 구간
            controller.ConfigureDissolve(
                health,
                autoBindHealthOnAwake,
                autoCollectRenderers,
                includeInactiveRenderers,
                targetRenderers,
                changeObjectLayerOnStart,
                includeChildrenLayerChange,
                dissolveLayer,
                dissolveRenderingLayerMask,
                dissolveStartDelay,
                dissolveDuration,
                dissolveStartValue,
                dissolveEndValue,
                restoreStateOnDisable,
                remapMainTexToBaseMap,
                remapColorToBaseColor,
                remapBumpToNormal,
                forceOpaqueBaseColorAlpha);
        }

        private void NormalizeDissolveRangeForward()
        {
            dissolveStartValue = Mathf.Clamp01(dissolveStartValue);
            dissolveEndValue = Mathf.Clamp01(dissolveEndValue);

            if (dissolveStartValue > dissolveEndValue)
            {
                var temp = dissolveStartValue;
                dissolveStartValue = dissolveEndValue;
                dissolveEndValue = temp;
            }
        }

        private CharacterSpecialEffectController GetOrCreateController()
        {
            // 캐시된 참조 우선 반환 경로
            if (cachedController != null)
            {
                return cachedController;
            }

            // 컴포넌트 미존재 시 신규 추가 경로
            if (!TryGetComponent(out cachedController))
            {
                cachedController = gameObject.AddComponent<CharacterSpecialEffectController>();
            }

            return cachedController;
        }

        private void SyncLayerIndexFromLegacyName()
        {
            // 인덱스 기설정 상태 스킵 가드
            // 레거시 보정 불필요 분기
            if (dissolveLayer >= 0)
            {
                return;
            }

            // 레거시 이름 미설정 상태 스킵 가드
            // 해석 대상 부재 분기
            if (string.IsNullOrEmpty(legacyDissolveLayerName))
            {
                return;
            }

            // 레거시 문자열 레이어명 -> 인덱스 해석 구간
            dissolveLayer = LayerMask.NameToLayer(legacyDissolveLayerName);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using LineworkLite.Common.Attributes;
using TH.Attribute;
using TH.Core.Pool;
using TH.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace TH.Rendering.Dissolve
{
    // 캐릭터 특수 이펙트 통합 제어 컴포넌트
    // 아웃라인/디졸브 단일 활성 상태 관리 허브
    [DisallowMultipleComponent]
    public sealed class CharacterSpecialEffectController : MonoBehaviour
    {
        // 현재 우선 적용 중인 특수 이펙트 상태 식별 열거형
        private enum ActiveEffectType
        {
            None = 0,
            Outline = 1,
            Dissolve = 2
        }

        [Header("Effect Toggle")]
        [SerializeField] private bool enableOutlineEffect = true;
        [SerializeField] private bool enableDissolveEffect = true;

        [Header("Binding")]
        [SerializeField] private Health health;
        [SerializeField] private bool autoBindHealthOnAwake = true;

        [Header("Dissolve Target")]
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Dissolve Layer Routing")]
        [SerializeField] private bool changeObjectLayerOnStart = true;
        [SerializeField] private bool includeChildrenLayerChange = true;
        [SerializeField, TH.Editor.Layer] private int dissolveLayer = -1;
        [FormerlySerializedAs("dissolveLayerName")]
        [SerializeField, HideInInspector] private string legacyDissolveLayerName = "DissolveOnly";
        [FormerlySerializedAs("dissolveRenderingLayerMask")]
        [SerializeField] [RenderingLayerMask] private uint skinnedDissolveRenderingLayerMask = 1u << 3;
        [SerializeField] [RenderingLayerMask] private uint meshDissolveRenderingLayerMask = 1u << 4;

        [Header("Dissolve Timeline")]
        [SerializeField, Min(0f)] private float dissolveStartDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float dissolveDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float dissolveStartValue = 0f;
        [SerializeField, Range(0f, 1f)] private float dissolveEndValue = 1f;
        [SerializeField] private bool restoreStateOnDisable = true;

        [Header("Dissolve Property Mapping")]
        [SerializeField] private bool remapMainTexToBaseMap = true;
        [SerializeField] private bool remapColorToBaseColor = true;
        [SerializeField] private bool remapBumpToNormal = true;
        [SerializeField] private bool forceOpaqueBaseColorAlpha = true;

        // 아웃라인 적용 대상 렌더러 캐시 목록
        private readonly List<Renderer> outlineRenderers = new List<Renderer>(8);
        // 아웃라인 적용 전 렌더링 레이어 마스크 원본값 캐시
        private readonly List<uint> outlineOriginalRenderingLayerMasks = new List<uint>(8);

        // 디졸브 런타임 대상 렌더러 목록
        private readonly List<Renderer> dissolveRuntimeRenderers = new List<Renderer>(8);
        // 디졸브 적용 전 렌더링 레이어 마스크 원본값 캐시
        private readonly List<uint> dissolveOriginalRenderingLayerMasks = new List<uint>(8);
        // 디졸브 적용 전 머티리얼 프로퍼티 블록 원본값 캐시
        private readonly List<MaterialPropertyBlock> dissolveOriginalPropertyBlocks = new List<MaterialPropertyBlock>(8);
        // 복원 시 원래 PropertyBlock 존재 여부를 분기하기 위한 캐시
        private readonly List<bool> dissolveOriginalHadPropertyBlocks = new List<bool>(8);

        // 디졸브 프레임 갱신용 작업 프로퍼티 블록 캐시
        private readonly List<MaterialPropertyBlock> dissolveWorkingPropertyBlocks = new List<MaterialPropertyBlock>(8);
        // 디졸브 중 변경된 레이어 대상 트랜스폼 목록
        private readonly List<Transform> dissolveLayerChangedTransforms = new List<Transform>(16);
        // 디졸브 중 변경 전 게임오브젝트 레이어 원본값 캐시
        private readonly List<int> dissolveOriginalLayers = new List<int>(16);

        // 디졸브 코루틴 핸들 캐시
        private Coroutine dissolveRoutine;
        // 풀 반환으로 인한 비활성화 시 1회성 상태 복원 스킵 플래그
        private bool skipRestoreOnDisableOnce;
        // 시작 지연 대기 객체 캐시
        private WaitForSeconds dissolveStartDelayWait;
        // 디졸브 상태 캡처 완료 여부 플래그
        private bool hasCapturedDissolveState;
        // 현재 활성 이펙트 상태
        private ActiveEffectType activeEffectType;
        // 현재 적용 중 아웃라인 비트마스크 캐시
        private uint activeOutlineMask;

        // 셰이더 프로퍼티 접근용 정적 해시 캐시
        private static readonly int DissolvePropertyId = Shader.PropertyToID("_Dissolve");
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int NormalMapPropertyId = Shader.PropertyToID("_NormalMap");
        private static readonly int BumpMapPropertyId = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicSmoothnessMapPropertyId = Shader.PropertyToID("_R_Metallic_G_Occulsion_A_Smoothness");
        private static readonly int MetallicGlossMapPropertyId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int NormalScalePropertyId = Shader.PropertyToID("_NormalScale");
        private static readonly int BumpScalePropertyId = Shader.PropertyToID("_BumpScale");
        private static readonly int TilingPropertyId = Shader.PropertyToID("_Tiling");
        private static readonly int OffsetPropertyId = Shader.PropertyToID("_Offest");

        public void ConfigureDefaults(string layerName, uint renderingLayerMask)
        {
            ConfigureDefaults(layerName, renderingLayerMask, renderingLayerMask);
        }

        public void ConfigureDefaults(string layerName, uint skinnedRenderingLayerMask, uint meshRenderingLayerMask)
        {
            legacyDissolveLayerName = layerName;
            dissolveLayer = LayerMask.NameToLayer(layerName);
            skinnedDissolveRenderingLayerMask = skinnedRenderingLayerMask;
            meshDissolveRenderingLayerMask = meshRenderingLayerMask;
            autoCollectRenderers = true;
            includeInactiveRenderers = true;
            changeObjectLayerOnStart = true;
            includeChildrenLayerChange = true;
            restoreStateOnDisable = true;
            RefreshDissolveDelayCache();
            NormalizeDissolveRangeForward();
        }


        public void ConfigureDissolve(
            Health configuredHealth,
            bool shouldAutoBindHealthOnAwake,
            bool shouldAutoCollectRenderers,
            bool shouldIncludeInactiveRenderers,
            Renderer[] configuredTargetRenderers,
            bool shouldChangeObjectLayerOnStart,
            bool shouldIncludeChildrenLayerChange,
            int configuredDissolveLayer,
            uint configuredDissolveRenderingLayerMask,
            float configuredDissolveStartDelay,
            float configuredDissolveDuration,
            float configuredDissolveStartValue,
            float configuredDissolveEndValue,
            bool shouldRestoreStateOnDisable,
            bool shouldRemapMainTexToBaseMap,
            bool shouldRemapColorToBaseColor,
            bool shouldRemapBumpToNormal,
            bool shouldForceOpaqueBaseColorAlpha)
        {
            // 런타임 재설정 시 기존 체력 이벤트 구독 정리 구간
            var previousHealth = health;
            if (isActiveAndEnabled && previousHealth != null)
            {
                previousHealth.OnDead -= HandleDead;
                previousHealth.OnRevived -= HandleRevived;
            }

            // 디졸브 동작 파라미터 동기화 구간
            health = configuredHealth;
            autoBindHealthOnAwake = shouldAutoBindHealthOnAwake;
            autoCollectRenderers = shouldAutoCollectRenderers;
            includeInactiveRenderers = shouldIncludeInactiveRenderers;
            targetRenderers = configuredTargetRenderers;
            changeObjectLayerOnStart = shouldChangeObjectLayerOnStart;
            includeChildrenLayerChange = shouldIncludeChildrenLayerChange;
            dissolveLayer = configuredDissolveLayer;
            legacyDissolveLayerName = dissolveLayer >= 0 ? LayerMask.LayerToName(dissolveLayer) : legacyDissolveLayerName;
            skinnedDissolveRenderingLayerMask = configuredDissolveRenderingLayerMask;
            meshDissolveRenderingLayerMask = configuredDissolveRenderingLayerMask;
            dissolveStartDelay = Mathf.Max(0f, configuredDissolveStartDelay);
            dissolveDuration = Mathf.Max(0.05f, configuredDissolveDuration);
            dissolveStartValue = Mathf.Clamp01(configuredDissolveStartValue);
            dissolveEndValue = Mathf.Clamp01(configuredDissolveEndValue);
            NormalizeDissolveRangeForward();
            restoreStateOnDisable = shouldRestoreStateOnDisable;
            remapMainTexToBaseMap = shouldRemapMainTexToBaseMap;
            remapColorToBaseColor = shouldRemapColorToBaseColor;
            remapBumpToNormal = shouldRemapBumpToNormal;
            forceOpaqueBaseColorAlpha = shouldForceOpaqueBaseColorAlpha;
            RefreshDissolveDelayCache();

            // 활성 상태 즉시 반영용 체력 이벤트 재구독 구간
            if (isActiveAndEnabled)
            {
                if (autoBindHealthOnAwake && health == null)
                {
                    TryGetComponent(out health);
                }

                if (health != null)
                {
                    health.OnDead += HandleDead;
                    health.OnRevived += HandleRevived;
                }
            }
        }

        public bool TryApplyOutline(uint outlineRenderingLayerMask)
        {
            // 아웃라인 기능 비활성 상태 가드
            if (!enableOutlineEffect)
            {
                ClearOutline();
                return false;
            }

            // 무효 마스크 입력 시 기존 아웃라인 해제 경로
            if (outlineRenderingLayerMask == 0)
            {
                ClearOutline();
                return false;
            }

            // 디졸브 우선순위 보장용 아웃라인 적용 차단 경로
            if (activeEffectType == ActiveEffectType.Dissolve)
            {
                return false;
            }

            // 동일 마스크 재요청 시 중복 적용 방지 경로
            if (activeEffectType == ActiveEffectType.Outline && activeOutlineMask == outlineRenderingLayerMask)
            {
                return true;
            }

            // 기존 이펙트 원복 후 아웃라인 신규 적용 준비 구간
            RestoreCurrentEffect();
            CollectOutlineRenderers();
            if (outlineRenderers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                // 렌더러별 원본값 저장 + OR 비트 적용 구간
                var renderer = outlineRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                outlineOriginalRenderingLayerMasks.Add(renderer.renderingLayerMask);
                renderer.renderingLayerMask |= outlineRenderingLayerMask;
            }

            activeOutlineMask = outlineRenderingLayerMask;
            activeEffectType = ActiveEffectType.Outline;
            return true;
        }

        public void ClearOutline()
        {
            // 아웃라인 활성 상태에서만 원복 수행 가드
            if (activeEffectType != ActiveEffectType.Outline)
            {
                return;
            }

            RestoreOutlineState();
        }

        [ContextMenu("Trigger Dissolve")]
        public void StartDeathDissolve()
        {
            // 디졸브 기능 비활성 상태 가드
            if (!enableDissolveEffect)
            {
                return;
            }

            // 중복 코루틴 기동 차단 가드
            if (dissolveRoutine != null)
            {
                return;
            }

            dissolveRoutine = StartCoroutine(CoDissolve());
        }

        private void Awake()
        {
            // 지연 대기 캐시 구성 초기화 구간
            RefreshDissolveDelayCache();

            // 체력 자동 바인딩 구간
            if (autoBindHealthOnAwake && health == null)
            {
                TryGetComponent(out health);
            }

            // 자동 수집 옵션 기반 렌더러 프리캐시 구간
            if (autoCollectRenderers)
            {
                CollectDissolveTargetRenderers();
            }
        }

        private void OnEnable()
        {
            // 비활성-활성 전환 시 잔여 상태 복원 경로
            if (restoreStateOnDisable)
            {
                RestoreCurrentEffect();
            }

            // 체력 이벤트 구독 구간
            if (health != null)
            {
                health.OnDead += HandleDead;
                health.OnRevived += HandleRevived;
            }
        }

        private void OnDisable()
        {
            // 체력 이벤트 구독 해제 구간
            if (health != null)
            {
                health.OnDead -= HandleDead;
                health.OnRevived -= HandleRevived;
            }

            // 코루틴 중단 및 핸들 초기화 구간
            if (dissolveRoutine != null)
            {
                StopCoroutine(dissolveRoutine);
                dissolveRoutine = null;
            }

            // 풀 반환 직전 비활성화 프레임에서는 복원 스킵(종료 깜빡임 방지)
            var shouldSkipRestore = skipRestoreOnDisableOnce;
            skipRestoreOnDisableOnce = false;

            // 비활성 시 원본 상태 복원 구간
            if (restoreStateOnDisable && !shouldSkipRestore)
            {
                RestoreCurrentEffect();
            }
        }

        private void HandleDead()
        {
            // 사망 이벤트 진입점 디졸브 시작 트리거
            StartDeathDissolve();
        }

        private void HandleRevived()
        {
            // 부활 이벤트 진입점 디졸브 중단 + 원복 처리
            if (dissolveRoutine != null)
            {
                StopCoroutine(dissolveRoutine);
                dissolveRoutine = null;
            }

            RestoreCurrentEffect();
        }

        private IEnumerator CoDissolve()
        {
            // 시작 지연 옵션 적용 구간
            if (dissolveStartDelayWait != null)
            {
                yield return dissolveStartDelayWait;
            }

            // 준비 단계 실패 시 코루틴 조기 종료 경로
            if (!PrepareStateForDissolve())
            {
                dissolveRoutine = null;
                yield break;
            }

            // 디졸브 시간 비율 기반 프레임 갱신 루프
            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / dissolveDuration);
                var dissolveValue = Mathf.Lerp(dissolveStartValue, dissolveEndValue, normalized);
                ApplyDissolveValue(dissolveValue);
                yield return null;
            }

            ApplyDissolveValue(dissolveEndValue);
            dissolveRoutine = null;
            // 연출 완료 후 오브젝트 종료 처리 구간
            CompleteDeathPresentation();
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

        private void RefreshDissolveDelayCache()
        {
            // 지연 시간 변경 반영용 WaitForSeconds 재생성 구간
            dissolveStartDelayWait = dissolveStartDelay > 0f
                ? new WaitForSeconds(dissolveStartDelay)
                : null;
        }

        private void CompleteDeathPresentation()
        {
            // 풀 반환 경로에서는 OnDisable 복원 1회 스킵으로 마지막 프레임 깜빡임 방지
            skipRestoreOnDisableOnce = true;

            // 풀 오브젝트 환경 릴리즈 연동 구간
            if (!TryGetComponent<IPoolObject>(out var poolObject))
            {
                skipRestoreOnDisableOnce = false;
                return;
            }

            poolObject.ReleaseSelf();
        }

        private bool PrepareStateForDissolve()
        {
            // 우선순위 규칙 보장을 위해 선행 이펙트를 복원
            RestoreCurrentEffect();
            // 최신 대상 렌더러를 수집
            CollectDissolveTargetRenderers();
            if (dissolveRuntimeRenderers.Count == 0)
            {
                return false;
            }

            // 인스펙터 레이어 인덱스 해석 + 레거시 이름 대입
            var dissolveLayerIndex = ResolveDissolveLayerIndex();
            if (changeObjectLayerOnStart && dissolveLayerIndex < 0)
            {
                Debug.LogWarning($"[{nameof(CharacterSpecialEffectController)}] Dissolve layer is not configured.", this);
            }

            for (int i = 0; i < dissolveRuntimeRenderers.Count; i++)
            {
                // 렌더러별 원본 상태 캡처 + 디졸브 PropertyBlock 적용
                var renderer = dissolveRuntimeRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                dissolveOriginalRenderingLayerMasks.Add(renderer.renderingLayerMask);
                dissolveOriginalHadPropertyBlocks.Add(renderer.HasPropertyBlock());

                var originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);
                dissolveOriginalPropertyBlocks.Add(originalBlock);

                var workingBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(workingBlock);

                var useMeshDissolveRoute = IsMeshDissolveRenderer(renderer);
                if (!useMeshDissolveRoute)
                {
                    MapSourceProperties(renderer, workingBlock);
                }

                workingBlock.SetFloat(DissolvePropertyId, dissolveStartValue);

                dissolveWorkingPropertyBlocks.Add(workingBlock);
                renderer.SetPropertyBlock(workingBlock);
                renderer.renderingLayerMask = useMeshDissolveRoute
                    ? ResolveMeshDissolveRenderingLayerMask()
                    : ResolveSkinnedDissolveRenderingLayerMask();
            }

            if (changeObjectLayerOnStart && dissolveLayerIndex >= 0)
            {
                // 오브젝트 레이어 일괄 적용
                CaptureAndApplyLayerState(dissolveLayerIndex);
            }

            // 디졸브 상태 캡처 완료 플래그 전환
            hasCapturedDissolveState = true;
            activeEffectType = ActiveEffectType.Dissolve;
            return true;
        }

        private void CollectOutlineRenderers()
        {
            // 이전 캐시 초기화 구간
            outlineRenderers.Clear();
            outlineOriginalRenderingLayerMasks.Clear();

            // 장비 외형 컨트롤러 기반 활성 렌더러 우선 수집 경로
            if (TryGetOutfitOutlineRenderers(gameObject, out var outfitRenderers))
            {
                for (int i = 0; i < outfitRenderers.Count; i++)
                {
                    var renderer = outfitRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    outlineRenderers.Add(renderer);
                }

                return;
            }

            // 폴백 경로 자식 렌더러 전체 수집 구간
            var renderers = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                outlineRenderers.Add(renderer);
            }
        }

        private void CollectDissolveTargetRenderers()
        {
            // 런타임 캐시 초기화 구간
            dissolveRuntimeRenderers.Clear();

            // 자동 수집 또는 수동 목록 미설정 시 전체 수집 경로
            if (autoCollectRenderers || targetRenderers == null || targetRenderers.Length == 0)
            {
                var renderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    dissolveRuntimeRenderers.Add(renderer);
                }

                return;
            }

            // 수동 지정 렌더러 목록 수집 경로
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                dissolveRuntimeRenderers.Add(renderer);
            }
        }

        private void CaptureAndApplyLayerState(int dissolveLayerIndex)
        {
            // 이전 레이어 변경 기록 초기화 구간
            dissolveLayerChangedTransforms.Clear();
            dissolveOriginalLayers.Clear();

            // 자식 포함 옵션 경로 전체 트랜스폼 레이어 변경 구간
            if (includeChildrenLayerChange)
            {
                var transforms = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    var current = transforms[i];
                    if (current == null)
                    {
                        continue;
                    }

                    dissolveLayerChangedTransforms.Add(current);
                    dissolveOriginalLayers.Add(current.gameObject.layer);
                    current.gameObject.layer = dissolveLayerIndex;
                }

                return;
            }

            // 루트 단일 레이어 변경 경로
            dissolveLayerChangedTransforms.Add(transform);
            dissolveOriginalLayers.Add(gameObject.layer);
            gameObject.layer = dissolveLayerIndex;
        }

        private void ApplyDissolveValue(float value)
        {
            // 작업 프로퍼티 블록 기반 디졸브 값 프레임 반영 루프
            for (int i = 0; i < dissolveRuntimeRenderers.Count; i++)
            {
                var renderer = dissolveRuntimeRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var block = dissolveWorkingPropertyBlocks[i];
                block.SetFloat(DissolvePropertyId, value);
                renderer.SetPropertyBlock(block);
            }
        }

        private void RestoreCurrentEffect()
        {
            // 활성 상태별 분기 원복 디스패치 구간
            if (activeEffectType == ActiveEffectType.Outline)
            {
                RestoreOutlineState();
            }
            else if (activeEffectType == ActiveEffectType.Dissolve)
            {
                RestoreDissolveState();
            }
        }

        private void RestoreOutlineState()
        {
            // 아웃라인 렌더링 레이어 원본값 복원 루프
            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                var renderer = outlineRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.renderingLayerMask = outlineOriginalRenderingLayerMasks[i];
            }

            outlineRenderers.Clear();
            outlineOriginalRenderingLayerMasks.Clear();
            activeOutlineMask = 0;

            // 활성 상태 초기화 구간
            if (activeEffectType == ActiveEffectType.Outline)
            {
                activeEffectType = ActiveEffectType.None;
            }
        }

        private void RestoreDissolveState()
        {
            // 캡처 미완료 상태 가드
            if (!hasCapturedDissolveState)
            {
                if (activeEffectType == ActiveEffectType.Dissolve)
                {
                    activeEffectType = ActiveEffectType.None;
                }

                return;
            }

            // 렌더러별 렌더링레이어 + PropertyBlock 원복 루프
            for (int i = 0; i < dissolveRuntimeRenderers.Count; i++)
            {
                var renderer = dissolveRuntimeRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.renderingLayerMask = dissolveOriginalRenderingLayerMasks[i];

                // 원래 PropertyBlock이 없던 렌더러는 null로 지워 SRP Batcher 복귀 가능 상태를 보장
                if (i < dissolveOriginalHadPropertyBlocks.Count && !dissolveOriginalHadPropertyBlocks[i])
                {
                    renderer.SetPropertyBlock(null);
                }
                else
                {
                    renderer.SetPropertyBlock(dissolveOriginalPropertyBlocks[i]);
                }
            }

            // 게임오브젝트 레이어 원복 루프
            for (int i = 0; i < dissolveLayerChangedTransforms.Count; i++)
            {
                var changedTransform = dissolveLayerChangedTransforms[i];
                if (changedTransform == null)
                {
                    continue;
                }

                changedTransform.gameObject.layer = dissolveOriginalLayers[i];
            }

            dissolveRuntimeRenderers.Clear();
            dissolveOriginalRenderingLayerMasks.Clear();
            dissolveOriginalPropertyBlocks.Clear();
            dissolveOriginalHadPropertyBlocks.Clear();
            dissolveWorkingPropertyBlocks.Clear();
            dissolveLayerChangedTransforms.Clear();
            dissolveOriginalLayers.Clear();
            hasCapturedDissolveState = false;

            // 활성 상태 초기화 구간
            if (activeEffectType == ActiveEffectType.Dissolve)
            {
                activeEffectType = ActiveEffectType.None;
            }
        }

        private static bool IsMeshDissolveRenderer(Renderer renderer)
        {
            return renderer is MeshRenderer && renderer is not SkinnedMeshRenderer;
        }

        private uint ResolveSkinnedDissolveRenderingLayerMask()
        {
            if (skinnedDissolveRenderingLayerMask != 0)
            {
                return skinnedDissolveRenderingLayerMask;
            }

            return meshDissolveRenderingLayerMask != 0
                ? meshDissolveRenderingLayerMask
                : 1u << 3;
        }

        private uint ResolveMeshDissolveRenderingLayerMask()
        {
            if (meshDissolveRenderingLayerMask != 0)
            {
                return meshDissolveRenderingLayerMask;
            }

            return ResolveSkinnedDissolveRenderingLayerMask();
        }

        private void MapSourceProperties(Renderer renderer, MaterialPropertyBlock block)
        {
            // 원본 머티리얼 기반 디졸브 셰이더 입력값 매핑 구간
            var sourceMaterial = renderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                return;
            }

            // 베이스 텍스처 및 타일링/오프셋 매핑 구간
            if (remapMainTexToBaseMap)
            {
                var baseMap = sourceMaterial.HasProperty(BaseMapPropertyId)
                    ? sourceMaterial.GetTexture(BaseMapPropertyId)
                    : sourceMaterial.HasProperty(MainTexPropertyId)
                        ? sourceMaterial.GetTexture(MainTexPropertyId)
                        : null;

                if (baseMap != null)
                {
                    block.SetTexture(BaseMapPropertyId, baseMap);
                }

                var scale = Vector2.one;
                var offset = Vector2.zero;
                if (sourceMaterial.HasProperty(BaseMapPropertyId))
                {
                    scale = sourceMaterial.GetTextureScale("_BaseMap");
                    offset = sourceMaterial.GetTextureOffset("_BaseMap");
                }
                else if (sourceMaterial.HasProperty(MainTexPropertyId))
                {
                    scale = sourceMaterial.GetTextureScale("_MainTex");
                    offset = sourceMaterial.GetTextureOffset("_MainTex");
                }

                block.SetVector(TilingPropertyId, new Vector4(scale.x, scale.y, 0f, 0f));
                block.SetVector(OffsetPropertyId, new Vector4(offset.x, offset.y, 0f, 0f));
            }

            // 베이스 컬러 매핑 및 알파 강제 보정 구간
            if (remapColorToBaseColor)
            {
                var mappedColor = Color.white;

                if (sourceMaterial.HasProperty(BaseColorPropertyId))
                {
                    mappedColor = sourceMaterial.GetColor(BaseColorPropertyId);
                }
                else if (sourceMaterial.HasProperty(ColorPropertyId))
                {
                    mappedColor = sourceMaterial.GetColor(ColorPropertyId);
                }

                if (forceOpaqueBaseColorAlpha)
                {
                    mappedColor.a = 1f;
                }

                block.SetColor(BaseColorPropertyId, mappedColor);
            }

            // 메탈릭/스무스니스 텍스처 채널 매핑 구간
            if (sourceMaterial.HasProperty(MetallicGlossMapPropertyId))
            {
                var metallicGlossMap = sourceMaterial.GetTexture(MetallicGlossMapPropertyId);
                if (metallicGlossMap != null)
                {
                    block.SetTexture(MetallicSmoothnessMapPropertyId, metallicGlossMap);
                }
            }

            // 노말맵 매핑 비활성 옵션 가드
            if (!remapBumpToNormal)
            {
                return;
            }

            // 노말맵 또는 범프맵 소스 매핑 구간
            if (sourceMaterial.HasProperty(NormalMapPropertyId))
            {
                var normalMap = sourceMaterial.GetTexture(NormalMapPropertyId);
                if (normalMap != null)
                {
                    block.SetTexture(NormalMapPropertyId, normalMap);
                }
            }
            else if (sourceMaterial.HasProperty(BumpMapPropertyId))
            {
                var bumpMap = sourceMaterial.GetTexture(BumpMapPropertyId);
                if (bumpMap != null)
                {
                    block.SetTexture(NormalMapPropertyId, bumpMap);
                }
            }

            // 범프 스케일 -> 노말 스케일 매핑 구간
            if (sourceMaterial.HasProperty(BumpScalePropertyId))
            {
                block.SetFloat(NormalScalePropertyId, sourceMaterial.GetFloat(BumpScalePropertyId));
            }
        }

        private static bool TryGetOutfitOutlineRenderers(GameObject target, out IReadOnlyList<Renderer> renderers)
        {
            // 대상 유효성 및 아웃핏 컨트롤러 탐색 구간
            renderers = null;
            if (target == null)
            {
                return false;
            }

            if (!target.TryGetComponent<EquipmentOutfitController>(out var outfitController))
            {
                outfitController = target.GetComponentInParent<EquipmentOutfitController>();
            }

            if (outfitController == null)
            {
                return false;
            }

            return outfitController.TryGetActiveOutlineRenderers(out renderers, out _);
        }
    

        private int ResolveDissolveLayerIndex()
        {
            // 인스펙터 레이어 인덱스 우선 사용 경로
            if (dissolveLayer >= 0 && dissolveLayer <= 31)
            {
                return dissolveLayer;
            }

            // 레거시 문자열 레이어명 폴백 해석 경로
            if (!string.IsNullOrEmpty(legacyDissolveLayerName))
            {
                var layerIndex = LayerMask.NameToLayer(legacyDissolveLayerName);
                if (layerIndex >= 0)
                {
                    dissolveLayer = layerIndex;
                    return layerIndex;
                }
            }

            // 해석 실패 시 미설정 값 반환 경로
            return -1;
        }
}
}

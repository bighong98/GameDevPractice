using System.Collections;
using System.Collections.Generic;
using LineworkLite.Common.Attributes;
using TH.Attribute;
using TH.Core.Pool;
using TH.Item;
using UnityEngine;

namespace TH.Rendering.Dissolve
{
    [DisallowMultipleComponent]
    public sealed class CharacterSpecialEffectController : MonoBehaviour
    {
        private enum ActiveEffectType
        {
            None = 0,
            Outline = 1,
            Dissolve = 2
        }

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
        [SerializeField] private string dissolveLayerName = "DissolveOnly";
        [SerializeField] [RenderingLayerMask] private uint dissolveRenderingLayerMask = 1u << 3;

        [Header("Dissolve Timeline")]
        [SerializeField, Min(0f)] private float dissolveStartDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float dissolveDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float dissolveStartValue = 1f;
        [SerializeField, Range(0f, 1f)] private float dissolveEndValue = 0f;
        [SerializeField] private bool restoreStateOnDisable = true;

        [Header("Dissolve Property Mapping")]
        [SerializeField] private bool remapMainTexToBaseMap = true;
        [SerializeField] private bool remapColorToBaseColor = true;
        [SerializeField] private bool remapBumpToNormal = true;
        [SerializeField] private bool forceOpaqueBaseColorAlpha = true;

        private readonly List<Renderer> outlineRenderers = new List<Renderer>(8);
        private readonly List<uint> outlineOriginalRenderingLayerMasks = new List<uint>(8);

        private readonly List<Renderer> dissolveRuntimeRenderers = new List<Renderer>(8);
        private readonly List<uint> dissolveOriginalRenderingLayerMasks = new List<uint>(8);
        private readonly List<MaterialPropertyBlock> dissolveOriginalPropertyBlocks = new List<MaterialPropertyBlock>(8);
        private readonly List<MaterialPropertyBlock> dissolveWorkingPropertyBlocks = new List<MaterialPropertyBlock>(8);
        private readonly List<Transform> dissolveLayerChangedTransforms = new List<Transform>(16);
        private readonly List<int> dissolveOriginalLayers = new List<int>(16);

        private Coroutine dissolveRoutine;
        private WaitForSeconds dissolveStartDelayWait;
        private bool hasCapturedDissolveState;
        private ActiveEffectType activeEffectType;
        private uint activeOutlineMask;

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
            dissolveLayerName = layerName;
            dissolveRenderingLayerMask = renderingLayerMask;
            autoCollectRenderers = true;
            includeInactiveRenderers = true;
            changeObjectLayerOnStart = true;
            includeChildrenLayerChange = true;
            restoreStateOnDisable = true;
            RefreshDissolveDelayCache();
        }

        public void ConfigureDissolve(
            Health configuredHealth,
            bool shouldAutoBindHealthOnAwake,
            bool shouldAutoCollectRenderers,
            bool shouldIncludeInactiveRenderers,
            Renderer[] configuredTargetRenderers,
            bool shouldChangeObjectLayerOnStart,
            bool shouldIncludeChildrenLayerChange,
            string configuredDissolveLayerName,
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
            var previousHealth = health;
            if (isActiveAndEnabled && previousHealth != null)
            {
                previousHealth.OnDead -= HandleDead;
                previousHealth.OnRevived -= HandleRevived;
            }

            health = configuredHealth;
            autoBindHealthOnAwake = shouldAutoBindHealthOnAwake;
            autoCollectRenderers = shouldAutoCollectRenderers;
            includeInactiveRenderers = shouldIncludeInactiveRenderers;
            targetRenderers = configuredTargetRenderers;
            changeObjectLayerOnStart = shouldChangeObjectLayerOnStart;
            includeChildrenLayerChange = shouldIncludeChildrenLayerChange;
            dissolveLayerName = configuredDissolveLayerName;
            dissolveRenderingLayerMask = configuredDissolveRenderingLayerMask;
            dissolveStartDelay = Mathf.Max(0f, configuredDissolveStartDelay);
            dissolveDuration = Mathf.Max(0.05f, configuredDissolveDuration);
            dissolveStartValue = Mathf.Clamp01(configuredDissolveStartValue);
            dissolveEndValue = Mathf.Clamp01(configuredDissolveEndValue);
            restoreStateOnDisable = shouldRestoreStateOnDisable;
            remapMainTexToBaseMap = shouldRemapMainTexToBaseMap;
            remapColorToBaseColor = shouldRemapColorToBaseColor;
            remapBumpToNormal = shouldRemapBumpToNormal;
            forceOpaqueBaseColorAlpha = shouldForceOpaqueBaseColorAlpha;
            RefreshDissolveDelayCache();

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
            if (outlineRenderingLayerMask == 0)
            {
                ClearOutline();
                return false;
            }

            if (activeEffectType == ActiveEffectType.Dissolve)
            {
                return false;
            }

            if (activeEffectType == ActiveEffectType.Outline && activeOutlineMask == outlineRenderingLayerMask)
            {
                return true;
            }

            RestoreCurrentEffect();
            CollectOutlineRenderers();
            if (outlineRenderers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < outlineRenderers.Count; i++)
            {
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
            if (activeEffectType != ActiveEffectType.Outline)
            {
                return;
            }

            RestoreOutlineState();
        }

        [ContextMenu("Trigger Dissolve")]
        public void StartDeathDissolve()
        {
            if (dissolveRoutine != null)
            {
                return;
            }

            dissolveRoutine = StartCoroutine(CoDissolve());
        }

        private void Awake()
        {
            RefreshDissolveDelayCache();

            if (autoBindHealthOnAwake && health == null)
            {
                TryGetComponent(out health);
            }

            if (autoCollectRenderers)
            {
                CollectDissolveTargetRenderers();
            }
        }

        private void OnEnable()
        {
            if (restoreStateOnDisable)
            {
                RestoreCurrentEffect();
            }

            if (health != null)
            {
                health.OnDead += HandleDead;
                health.OnRevived += HandleRevived;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDead -= HandleDead;
                health.OnRevived -= HandleRevived;
            }

            if (dissolveRoutine != null)
            {
                StopCoroutine(dissolveRoutine);
                dissolveRoutine = null;
            }

            if (restoreStateOnDisable)
            {
                RestoreCurrentEffect();
            }
        }

        private void HandleDead()
        {
            StartDeathDissolve();
        }

        private void HandleRevived()
        {
            if (dissolveRoutine != null)
            {
                StopCoroutine(dissolveRoutine);
                dissolveRoutine = null;
            }

            RestoreCurrentEffect();
        }

        private IEnumerator CoDissolve()
        {
            if (dissolveStartDelayWait != null)
            {
                yield return dissolveStartDelayWait;
            }

            if (!PrepareStateForDissolve())
            {
                dissolveRoutine = null;
                yield break;
            }

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
            CompleteDeathPresentation();
        }

        private void RefreshDissolveDelayCache()
        {
            dissolveStartDelayWait = dissolveStartDelay > 0f
                ? new WaitForSeconds(dissolveStartDelay)
                : null;
        }

        private void CompleteDeathPresentation()
        {
            if (!TryGetComponent<IPoolObject>(out var poolObject))
            {
                return;
            }

            poolObject.ReleaseSelf();
        }

        private bool PrepareStateForDissolve()
        {
            RestoreCurrentEffect();
            CollectDissolveTargetRenderers();
            if (dissolveRuntimeRenderers.Count == 0)
            {
                return false;
            }

            var dissolveLayerIndex = LayerMask.NameToLayer(dissolveLayerName);
            if (changeObjectLayerOnStart && dissolveLayerIndex < 0)
            {
                Debug.LogWarning($"[{nameof(CharacterSpecialEffectController)}] Layer '{dissolveLayerName}' does not exist.", this);
            }

            for (int i = 0; i < dissolveRuntimeRenderers.Count; i++)
            {
                var renderer = dissolveRuntimeRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                dissolveOriginalRenderingLayerMasks.Add(renderer.renderingLayerMask);

                var originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);
                dissolveOriginalPropertyBlocks.Add(originalBlock);

                var workingBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(workingBlock);
                MapSourceProperties(renderer, workingBlock);
                workingBlock.SetFloat(DissolvePropertyId, dissolveStartValue);

                dissolveWorkingPropertyBlocks.Add(workingBlock);
                renderer.SetPropertyBlock(workingBlock);
                renderer.renderingLayerMask = dissolveRenderingLayerMask;
            }

            if (changeObjectLayerOnStart && dissolveLayerIndex >= 0)
            {
                CaptureAndApplyLayerState(dissolveLayerIndex);
            }

            hasCapturedDissolveState = true;
            activeEffectType = ActiveEffectType.Dissolve;
            return true;
        }

        private void CollectOutlineRenderers()
        {
            outlineRenderers.Clear();
            outlineOriginalRenderingLayerMasks.Clear();

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
            dissolveRuntimeRenderers.Clear();

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
            dissolveLayerChangedTransforms.Clear();
            dissolveOriginalLayers.Clear();

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

            dissolveLayerChangedTransforms.Add(transform);
            dissolveOriginalLayers.Add(gameObject.layer);
            gameObject.layer = dissolveLayerIndex;
        }

        private void ApplyDissolveValue(float value)
        {
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

            if (activeEffectType == ActiveEffectType.Outline)
            {
                activeEffectType = ActiveEffectType.None;
            }
        }

        private void RestoreDissolveState()
        {
            if (!hasCapturedDissolveState)
            {
                if (activeEffectType == ActiveEffectType.Dissolve)
                {
                    activeEffectType = ActiveEffectType.None;
                }

                return;
            }

            for (int i = 0; i < dissolveRuntimeRenderers.Count; i++)
            {
                var renderer = dissolveRuntimeRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.renderingLayerMask = dissolveOriginalRenderingLayerMasks[i];
                renderer.SetPropertyBlock(dissolveOriginalPropertyBlocks[i]);
            }

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
            dissolveWorkingPropertyBlocks.Clear();
            dissolveLayerChangedTransforms.Clear();
            dissolveOriginalLayers.Clear();
            hasCapturedDissolveState = false;

            if (activeEffectType == ActiveEffectType.Dissolve)
            {
                activeEffectType = ActiveEffectType.None;
            }
        }

        private void MapSourceProperties(Renderer renderer, MaterialPropertyBlock block)
        {
            var sourceMaterial = renderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                return;
            }

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

            if (sourceMaterial.HasProperty(MetallicGlossMapPropertyId))
            {
                var metallicGlossMap = sourceMaterial.GetTexture(MetallicGlossMapPropertyId);
                if (metallicGlossMap != null)
                {
                    block.SetTexture(MetallicSmoothnessMapPropertyId, metallicGlossMap);
                }
            }

            if (!remapBumpToNormal)
            {
                return;
            }

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

            if (sourceMaterial.HasProperty(BumpScalePropertyId))
            {
                block.SetFloat(NormalScalePropertyId, sourceMaterial.GetFloat(BumpScalePropertyId));
            }
        }

        private static bool TryGetOutfitOutlineRenderers(GameObject target, out IReadOnlyList<Renderer> renderers)
        {
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
    }
}

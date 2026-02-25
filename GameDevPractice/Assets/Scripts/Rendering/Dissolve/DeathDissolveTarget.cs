using LineworkLite.Common.Attributes;
using TH.Attribute;
using UnityEngine;

namespace TH.Rendering.Dissolve
{
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
        [SerializeField] private string dissolveLayerName = "DissolveOnly";
        [SerializeField] [RenderingLayerMask] private uint dissolveRenderingLayerMask = 1u << 3;

        [Header("Timeline")]
        [SerializeField, Min(0f)] private float dissolveStartDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float dissolveDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float dissolveStartValue = 1f;
        [SerializeField, Range(0f, 1f)] private float dissolveEndValue = 0f;
        [SerializeField] private bool restoreStateOnDisable = true;

        [Header("Property Mapping")]
        [SerializeField] private bool remapMainTexToBaseMap = true;
        [SerializeField] private bool remapColorToBaseColor = true;
        [SerializeField] private bool remapBumpToNormal = true;
        [SerializeField] private bool forceOpaqueBaseColorAlpha = true;

        private CharacterSpecialEffectController cachedController;

        public void ConfigureDefaults(string layerName, uint renderingLayerMask)
        {
            dissolveLayerName = layerName;
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
            ApplyToController();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyToController();
            }
        }

        [ContextMenu("Trigger Dissolve")]
        public void StartDissolve()
        {
            ApplyToController();
            if (cachedController != null)
            {
                cachedController.StartDeathDissolve();
            }
        }

        private void ApplyToController()
        {
            var controller = GetOrCreateController();
            if (controller == null)
            {
                return;
            }

            controller.ConfigureDissolve(
                health,
                autoBindHealthOnAwake,
                autoCollectRenderers,
                includeInactiveRenderers,
                targetRenderers,
                changeObjectLayerOnStart,
                includeChildrenLayerChange,
                dissolveLayerName,
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

        private CharacterSpecialEffectController GetOrCreateController()
        {
            if (cachedController != null)
            {
                return cachedController;
            }

            if (!TryGetComponent(out cachedController))
            {
                cachedController = gameObject.AddComponent<CharacterSpecialEffectController>();
            }

            return cachedController;
        }
    }
}

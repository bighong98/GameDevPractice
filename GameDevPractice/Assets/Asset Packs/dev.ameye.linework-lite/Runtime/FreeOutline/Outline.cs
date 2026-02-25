using LineworkLite.Common.Attributes;
using LineworkLite.Common.Utils;
using UnityEngine;

namespace LineworkLite.FreeOutline
{
    public class Outline : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material material;
        [SerializeField, HideInInspector] private bool isActive = true;

        [RenderingLayerMask]
        public uint RenderingLayer = 1;
        public LayerMask layerMask = ~0;
        public OutlineRenderQueue renderQueue = OutlineRenderQueue.Opaque;
        public Occlusion occlusion = Occlusion.WhenNotOccluded;
        public MaskingStrategy maskingStrategy = MaskingStrategy.Stencil;
        [ColorUsage(true, true)] public Color color = Color.green;
        public bool enableOcclusion = false;
        [ColorUsage(true, true)] public Color occludedColor = Color.red;
        public BlendingMode blendMode = BlendingMode.Alpha;
        public ExtrusionMethod extrusionMethod = ExtrusionMethod.ClipSpaceNormalVector;
        public Scaling scaling;
        [Range(0.0f, 100.0f)] public float width = 20.0f;
        [Range(0.0f, 100.0f)] public float minWidth = 0.0f;
        public bool scaleWithResolution;
        public Resolution referenceResolution;
        public float customResolution;
        public MaterialType materialType;
        public Material customMaterial;

        private void OnEnable()
        {
            RenderingLayer = NormalizeSingleRenderingLayer(RenderingLayer);
            EnsureMaterialsAreInitialized();
        }

        private void OnValidate()
        {
            RenderingLayer = NormalizeSingleRenderingLayer(RenderingLayer);
        }

        private static uint NormalizeSingleRenderingLayer(uint renderingLayerMask)
        {
            if (renderingLayerMask == 0)
            {
                return 1u;
            }

            for (var i = 0; i < 32; i++)
            {
                var layerBit = 1u << i;
                if ((renderingLayerMask & layerBit) != 0)
                {
                    return layerBit;
                }
            }

            return 1u;
        }


        private void EnsureMaterialsAreInitialized()
        {
            if (material == null)
            {
                var shader = Shader.Find(ShaderPath.Outline);
                if (shader != null)
                {
                    material = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
        }

        public void AssignMaterials(Material copyFrom)
        {
            EnsureMaterialsAreInitialized();
            material.CopyPropertiesFromMaterial(copyFrom);
        }

        public bool IsActive()
        {
            return isActive;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }

        public void Cleanup()
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }
    }
}

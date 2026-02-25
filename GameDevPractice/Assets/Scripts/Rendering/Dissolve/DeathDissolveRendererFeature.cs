using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TH.Rendering.Dissolve
{
    [DisallowMultipleRendererFeature("Death Dissolve")]
    public sealed class DeathDissolveRendererFeature : ScriptableRendererFeature
    {
        private const string DefaultDissolveShaderName = "Shader Graphs/Dissolve/Dissolve_Metallic";

        [SerializeField] private DeathDissolvePassSettings settings = new DeathDissolvePassSettings();
        [SerializeField] private bool renderInSceneView = true;
        [SerializeField] private bool renderInGame = true;

        private DeathDissolveRenderPass pass;

        public DeathDissolvePassSettings Settings => settings;

        public void ConfigureDefaults(
            LayerMask layerMask,
            uint renderingLayerMask,
            Shader dissolveShader,
            Material dissolveMaterial = null)
        {
            settings.layerMask = layerMask;
            settings.renderingLayerMask = renderingLayerMask;
            settings.injectionPoint = RenderPassEvent.AfterRenderingOpaques;
            settings.renderQueueType = RenderQueueType.Opaque;

            if (dissolveMaterial != null)
            {
                settings.overrideMode = DeathDissolveOverrideMode.Material;
                settings.overrideMaterial = dissolveMaterial;
                settings.overrideMaterialPassIndex = 0;
                settings.overrideShader = null;
                settings.overrideShaderPassIndex = 0;
                return;
            }

            settings.overrideMode = DeathDissolveOverrideMode.Shader;
            settings.overrideShader = dissolveShader;
            settings.overrideShaderPassIndex = 0;
            settings.overrideMaterial = null;
            settings.overrideMaterialPassIndex = 0;
        }

        public override void Create()
        {
            settings ??= new DeathDissolvePassSettings();
            TryAssignDefaultShader();

            pass ??= new DeathDissolveRenderPass(settings);
            pass.Configure(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || !ShouldRenderForCamera(renderingData.cameraData.cameraType))
            {
                return;
            }

            pass.Configure(settings);
            renderer.EnqueuePass(pass);
        }

        private void TryAssignDefaultShader()
        {
            if (settings.overrideMode != DeathDissolveOverrideMode.Shader || settings.overrideShader != null)
            {
                return;
            }

            settings.overrideShader = Shader.Find(DefaultDissolveShaderName);
        }

        private bool ShouldRenderForCamera(CameraType cameraType)
        {
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return false;
            }

            if (cameraType == CameraType.SceneView)
            {
                return renderInSceneView;
            }

            return renderInGame;
        }
    }
}

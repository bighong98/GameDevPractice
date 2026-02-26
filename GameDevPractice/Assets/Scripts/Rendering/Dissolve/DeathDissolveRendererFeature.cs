using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TH.Rendering.Dissolve
{
    // 디졸브 렌더 패스 등록/카메라별 실행 제어 피처
    [DisallowMultipleRendererFeature("Death Dissolve")]
    public sealed class DeathDissolveRendererFeature : ScriptableRendererFeature
    {
        private const string DefaultDissolveShaderName = "Shader Graphs/Dissolve/Dissolve_Metallic";

        [Header("Skinned Pass")]
        [SerializeField] private DeathDissolvePassSettings skinnedSettings = new DeathDissolvePassSettings
        {
            profilerTag = "DeathDissolveSkinnedPass"
        };

        [Header("Mesh Pass")]
        [SerializeField] private DeathDissolvePassSettings meshSettings = new DeathDissolvePassSettings
        {
            profilerTag = "DeathDissolveMeshPass"
        };

        [SerializeField] private bool renderInSceneView = true;
        [SerializeField] private bool renderInGame = true;

        private DeathDissolveRenderPass skinnedPass;
        private DeathDissolveRenderPass meshPass;

        public DeathDissolvePassSettings SkinnedSettings => skinnedSettings;

        public DeathDissolvePassSettings MeshSettings => meshSettings;

        public void ConfigureDefaults(
            LayerMask layerMask,
            uint skinnedRenderingLayerMask,
            uint meshRenderingLayerMask,
            Shader dissolveShader,
            Material skinnedDissolveMaterial = null,
            Material meshDissolveMaterial = null)
        {
            ConfigurePassDefaults(skinnedSettings, layerMask, skinnedRenderingLayerMask, dissolveShader, skinnedDissolveMaterial);
            ConfigurePassDefaults(meshSettings, layerMask, meshRenderingLayerMask, dissolveShader, meshDissolveMaterial);
        }

        public override void Create()
        {
            skinnedSettings ??= new DeathDissolvePassSettings { profilerTag = "DeathDissolveSkinnedPass" };
            meshSettings ??= new DeathDissolvePassSettings { profilerTag = "DeathDissolveMeshPass" };

            TryAssignDefaultShader(skinnedSettings);
            TryAssignDefaultShader(meshSettings);

            skinnedPass ??= new DeathDissolveRenderPass(skinnedSettings);
            skinnedPass.Configure(skinnedSettings);

            meshPass ??= new DeathDissolveRenderPass(meshSettings);
            meshPass.Configure(meshSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!ShouldRenderForCamera(renderingData.cameraData.cameraType))
            {
                return;
            }

            if (skinnedPass != null)
            {
                skinnedPass.Configure(skinnedSettings);
                renderer.EnqueuePass(skinnedPass);
            }

            if (meshPass != null)
            {
                meshPass.Configure(meshSettings);
                renderer.EnqueuePass(meshPass);
            }
        }

        private static void ConfigurePassDefaults(
            DeathDissolvePassSettings targetSettings,
            LayerMask layerMask,
            uint renderingLayerMask,
            Shader dissolveShader,
            Material dissolveMaterial)
        {
            targetSettings.layerMask = layerMask;
            targetSettings.renderingLayerMask = renderingLayerMask;
            targetSettings.injectionPoint = RenderPassEvent.AfterRenderingOpaques;
            targetSettings.renderQueueType = RenderQueueType.Opaque;

            if (dissolveMaterial != null)
            {
                targetSettings.overrideMode = DeathDissolveOverrideMode.Material;
                targetSettings.overrideMaterial = dissolveMaterial;
                targetSettings.overrideMaterialPassIndex = 0;
                targetSettings.overrideShader = null;
                targetSettings.overrideShaderPassIndex = 0;
                return;
            }

            targetSettings.overrideMode = DeathDissolveOverrideMode.Shader;
            targetSettings.overrideShader = dissolveShader;
            targetSettings.overrideShaderPassIndex = 0;
            targetSettings.overrideMaterial = null;
            targetSettings.overrideMaterialPassIndex = 0;
        }

        private static void TryAssignDefaultShader(DeathDissolvePassSettings targetSettings)
        {
            if (targetSettings.overrideMode != DeathDissolveOverrideMode.Shader || targetSettings.overrideShader != null)
            {
                return;
            }

            targetSettings.overrideShader = Shader.Find(DefaultDissolveShaderName);
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

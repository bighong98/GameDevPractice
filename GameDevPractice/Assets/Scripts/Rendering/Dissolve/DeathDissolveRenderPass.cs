using System;
using System.Collections.Generic;
using LineworkLite.Common.Attributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TH.Rendering.Dissolve
{
    [Serializable]
    public enum DeathDissolveOverrideMode
    {
        Shader,
        Material
    }

    [Serializable]
    public sealed class DeathDissolvePassSettings
    {
        public string profilerTag = "DeathDissolvePass";
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;
        public RenderQueueType renderQueueType = RenderQueueType.Opaque;
        public LayerMask layerMask = 0;
        [RenderingLayerMask] public uint renderingLayerMask = uint.MaxValue;
        public DeathDissolveOverrideMode overrideMode = DeathDissolveOverrideMode.Shader;
        public Shader overrideShader;
        public int overrideShaderPassIndex = 0;
        public Material overrideMaterial;
        public int overrideMaterialPassIndex = 0;
        public string[] shaderTagNames =
        {
            "UniversalForward",
            "UniversalForwardOnly",
            "SRPDefaultUnlit",
            "LightweightForward"
        };
    }

    public sealed class DeathDissolveRenderPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            internal RendererListHandle rendererListHandle;
        }

        private readonly List<ShaderTagId> shaderTagIds = new List<ShaderTagId>(4);

        private DeathDissolvePassSettings settings;
        private FilteringSettings filteringSettings;
        private RenderQueueType renderQueueType;

        public DeathDissolveRenderPass(DeathDissolvePassSettings settings)
        {
            Configure(settings);
        }

        public void Configure(DeathDissolvePassSettings newSettings)
        {
            settings = newSettings ?? new DeathDissolvePassSettings();
            renderPassEvent = settings.injectionPoint;
            profilingSampler = new ProfilingSampler(settings.profilerTag);

            renderQueueType = settings.renderQueueType;
            var renderQueueRange = renderQueueType == RenderQueueType.Transparent
                ? RenderQueueRange.transparent
                : RenderQueueRange.opaque;

            filteringSettings = new FilteringSettings(renderQueueRange, settings.layerMask)
            {
                renderingLayerMask = settings.renderingLayerMask
            };

            BuildShaderTagIds(settings.shaderTagNames);
        }

[Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!HasValidOverride())
            {
                return;
            }

            var sortingCriteria = renderQueueType == RenderQueueType.Transparent
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(ref renderingData, sortingCriteria);
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
        }

public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!HasValidOverride())
            {
                return;
            }

            var universalRenderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>(settings.profilerTag, out var passData, profilingSampler);

            var sortingCriteria = renderQueueType == RenderQueueType.Transparent
                ? SortingCriteria.CommonTransparent
                : cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(universalRenderingData, cameraData, lightData, sortingCriteria);
            var localFilteringSettings = filteringSettings;
            var rendererListParams = new RendererListParams(universalRenderingData.cullResults, drawSettings, localFilteringSettings);
            passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

            if (!passData.rendererListHandle.IsValid())
            {
                return;
            }

            builder.UseRendererList(passData.rendererListHandle);
            // Dissolve pass overlays onto existing camera color/depth, so both buffers must be readable.
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(data.rendererListHandle);
            });
        }

        private bool HasValidOverride()
        {
            return settings.overrideMode switch
            {
                DeathDissolveOverrideMode.Shader => settings.overrideShader != null,
                DeathDissolveOverrideMode.Material => settings.overrideMaterial != null,
                _ => false
            };
        }

private DrawingSettings CreateDrawingSettings(ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            var drawingSettings = CreateDrawingSettings(shaderTagIds, ref renderingData, sortingCriteria);
            ApplyOverride(ref drawingSettings);
            return drawingSettings;
        }

        private DrawingSettings CreateDrawingSettings(
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            SortingCriteria sortingCriteria)
        {
            var drawingSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
            ApplyOverride(ref drawingSettings);
            return drawingSettings;
        }

        private void ApplyOverride(ref DrawingSettings drawingSettings)
        {
            if (settings.overrideMode == DeathDissolveOverrideMode.Shader)
            {
                drawingSettings.overrideShader = settings.overrideShader;
                drawingSettings.overrideShaderPassIndex = settings.overrideShaderPassIndex;
                drawingSettings.overrideMaterial = null;
                drawingSettings.overrideMaterialPassIndex = 0;
                return;
            }

            drawingSettings.overrideMaterial = settings.overrideMaterial;
            drawingSettings.overrideMaterialPassIndex = settings.overrideMaterialPassIndex;
            drawingSettings.overrideShader = null;
            drawingSettings.overrideShaderPassIndex = 0;
        }

        private void BuildShaderTagIds(string[] tagNames)
        {
            shaderTagIds.Clear();

            if (tagNames != null)
            {
                foreach (var tagName in tagNames)
                {
                    if (string.IsNullOrWhiteSpace(tagName))
                    {
                        continue;
                    }

                    shaderTagIds.Add(new ShaderTagId(tagName));
                }
            }

            if (shaderTagIds.Count > 0)
            {
                return;
            }

            shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagIds.Add(new ShaderTagId("LightweightForward"));
        }
    }
}

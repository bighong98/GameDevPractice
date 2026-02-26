using System;
using System.Collections.Generic;
using LineworkLite.Common.Attributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TH.Rendering.Dissolve
{
    // 디졸브 패스 오버라이드 방식 선택 열거형
    [Serializable]
    public enum DeathDissolveOverrideMode
    {
        Shader,
        Material
    }

    // 디졸브 렌더 패스 직렬화 설정 데이터
    [Serializable]
    public sealed class DeathDissolvePassSettings
    {
        // 프로파일러 샘플 태그
        public string profilerTag = "DeathDissolvePass";
        // 패스 삽입 지점
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;
        // 렌더 큐 타입 필터
        public RenderQueueType renderQueueType = RenderQueueType.Opaque;
        // 게임오브젝트 레이어 필터
        public LayerMask layerMask = 0;
        // 렌더링 레이어 마스크 필터
        [RenderingLayerMask] public uint renderingLayerMask = uint.MaxValue;
        // 오버라이드 리소스 선택 모드
        public DeathDissolveOverrideMode overrideMode = DeathDissolveOverrideMode.Shader;
        // 셰이더 오버라이드 리소스
        public Shader overrideShader;
        // 셰이더 패스 인덱스
        public int overrideShaderPassIndex = 0;
        // 머티리얼 오버라이드 리소스
        public Material overrideMaterial;
        // 머티리얼 패스 인덱스
        public int overrideMaterialPassIndex = 0;
        // 드로우 대상 셰이더 태그 목록
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
        // RenderGraph 패스 데이터 컨테이너
        private sealed class PassData
        {
            internal RendererListHandle rendererListHandle;
        }

        // 드로우 셰이더 태그 캐시 목록
        private readonly List<ShaderTagId> shaderTagIds = new List<ShaderTagId>(4);

        // 현재 패스 설정 참조
        private DeathDissolvePassSettings settings;
        // 레이어/렌더큐 필터 설정
        private FilteringSettings filteringSettings;
        // 렌더 큐 타입 캐시
        private RenderQueueType renderQueueType;

        public DeathDissolveRenderPass(DeathDissolvePassSettings settings)
        {
            // 생성 시점 설정 동기화 구간
            Configure(settings);
        }

        public void Configure(DeathDissolvePassSettings newSettings)
        {
            // 설정 널 가드 + 기본값 폴백
            settings = newSettings ?? new DeathDissolvePassSettings();
            renderPassEvent = settings.injectionPoint;
            profilingSampler = new ProfilingSampler(settings.profilerTag);

            // 렌더 큐 타입별 큐 범위 선택
            renderQueueType = settings.renderQueueType;
            var renderQueueRange = renderQueueType == RenderQueueType.Transparent
                ? RenderQueueRange.transparent
                : RenderQueueRange.opaque;

            // 필터링 설정 구성
            filteringSettings = new FilteringSettings(renderQueueRange, settings.layerMask)
            {
                renderingLayerMask = settings.renderingLayerMask
            };

            // 드로우 셰이더 태그 목록 재구성
            BuildShaderTagIds(settings.shaderTagNames);
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 오버라이드 리소스 유효성 가드
            if (!HasValidOverride())
            {
                return;
            }

            // 큐 타입별 정렬 기준 선택
            var sortingCriteria = renderQueueType == RenderQueueType.Transparent
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;

            // 드로우 설정 생성 후 필터 기반 드로우 호출
            var drawSettings = CreateDrawingSettings(ref renderingData, sortingCriteria);
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // 오버라이드 리소스 유효성 가드
            if (!HasValidOverride())
            {
                return;
            }

            // 프레임 데이터 핸들 조회
            var universalRenderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            // Raster RenderGraph 패스 생성
            using var builder = renderGraph.AddRasterRenderPass<PassData>(settings.profilerTag, out var passData, profilingSampler);

            // 큐 타입별 정렬 기준 선택
            var sortingCriteria = renderQueueType == RenderQueueType.Transparent
                ? SortingCriteria.CommonTransparent
                : cameraData.defaultOpaqueSortFlags;

            // 드로우/렌더러리스트 구성
            var drawSettings = CreateDrawingSettings(universalRenderingData, cameraData, lightData, sortingCriteria);
            var localFilteringSettings = filteringSettings;
            var rendererListParams = new RendererListParams(universalRenderingData.cullResults, drawSettings, localFilteringSettings);
            passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

            // 유효하지 않은 렌더러리스트 조기 종료
            if (!passData.rendererListHandle.IsValid())
            {
                return;
            }

            builder.UseRendererList(passData.rendererListHandle);
            // Dissolve pass overlays onto existing camera color/depth, so both buffers must be readable.
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

            // 렌더러리스트 드로우 실행 함수 바인딩
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(data.rendererListHandle);
            });
        }

        private bool HasValidOverride()
        {
            // 오버라이드 모드별 유효 리소스 판정
            return settings.overrideMode switch
            {
                DeathDissolveOverrideMode.Shader => settings.overrideShader != null,
                DeathDissolveOverrideMode.Material => settings.overrideMaterial != null,
                _ => false
            };
        }

        private DrawingSettings CreateDrawingSettings(ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            // 레거시 Execute 경로 드로우 설정 생성
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
            // RenderGraph 경로 드로우 설정 생성
            var drawingSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
            ApplyOverride(ref drawingSettings);
            return drawingSettings;
        }

        private void ApplyOverride(ref DrawingSettings drawingSettings)
        {
            // 셰이더 오버라이드 경로
            if (settings.overrideMode == DeathDissolveOverrideMode.Shader)
            {
                drawingSettings.overrideShader = settings.overrideShader;
                drawingSettings.overrideShaderPassIndex = settings.overrideShaderPassIndex;
                drawingSettings.overrideMaterial = null;
                drawingSettings.overrideMaterialPassIndex = 0;
                return;
            }

            // 머티리얼 오버라이드 경로
            drawingSettings.overrideMaterial = settings.overrideMaterial;
            drawingSettings.overrideMaterialPassIndex = settings.overrideMaterialPassIndex;
            drawingSettings.overrideShader = null;
            drawingSettings.overrideShaderPassIndex = 0;
        }

        private void BuildShaderTagIds(string[] tagNames)
        {
            // 기존 태그 캐시 초기화
            shaderTagIds.Clear();

            if (tagNames != null)
            {
                foreach (var tagName in tagNames)
                {
                    // 공백 태그 스킵
                    if (string.IsNullOrWhiteSpace(tagName))
                    {
                        continue;
                    }

                    shaderTagIds.Add(new ShaderTagId(tagName));
                }
            }

            // 사용자 태그 존재 시 기본 태그 주입 생략
            if (shaderTagIds.Count > 0)
            {
                return;
            }

            // 폴백 기본 태그 목록 주입
            shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagIds.Add(new ShaderTagId("LightweightForward"));
        }
    }
}

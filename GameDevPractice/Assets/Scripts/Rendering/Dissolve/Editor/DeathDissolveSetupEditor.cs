#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TH.Attribute;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TH.Rendering.Dissolve.Editor
{
    // 디졸브 렌더링 파이프라인 + 대상 컴포넌트 자동 설정 유틸리티
    public static class DeathDissolveSetupEditor
    {
        // 디졸브 라우팅 전용 프로젝트 레이어명
        private const string DissolveLayerName = "DissolveOnly";
        // 렌더링 레이어 비트 인덱스 상수
        private const int SkinnedDissolveRenderingLayerBitIndex = 3;
        private const int MeshDissolveRenderingLayerBitIndex = 4;
        // 디졸브 렌더링 레이어 마스크 상수
        private const uint SkinnedDissolveRenderingLayerMask = 1u << SkinnedDissolveRenderingLayerBitIndex;
        private const uint MeshDissolveRenderingLayerMask = 1u << MeshDissolveRenderingLayerBitIndex;

        // 디졸브 오버라이드 머티리얼 경로 상수
        private const string DissolveMaterialPath = "Assets/Asset Packs/VFX/ShaderGraph_Dissolve/URP/Materials/Shader Graphs_Dissolve_Dissolve_Metallic.mat";
        private const string MeshInstancedDissolveMaterialPath = "Assets/Asset Packs/VFX/ShaderGraph_Dissolve/URP/Materials/Shader Graphs_Dissolve_Dissolve_Metallic_Instanced.mat";

        // 프로젝트 타깃 URP 렌더러 데이터 경로 목록
        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Tools/Rendering/Death Dissolve/Setup All")]
        public static void SetupAll()
        {
            // 렌더러 기능 설정 + 씬 대상 컴포넌트 부착 일괄 실행 경로
            ConfigureUrpRenderers();
            AttachTargetsToActiveSceneEnemies();
        }

        [MenuItem("Tools/Rendering/Death Dissolve/Configure URP Renderers")]
        public static void ConfigureUrpRenderers()
        {
            // 디졸브 전용 프로젝트 레이어 확보 구간
            var dissolveLayerIndex = EnsureLayer(DissolveLayerName);
            if (dissolveLayerIndex < 0)
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Failed to allocate layer '{DissolveLayerName}'.");
                return;
            }

            // 디졸브 셰이더/머티리얼 로드 구간
            var skinnedDissolveMaterial = AssetDatabase.LoadAssetAtPath<Material>(DissolveMaterialPath);
            var meshDissolveMaterial = AssetDatabase.LoadAssetAtPath<Material>(MeshInstancedDissolveMaterialPath);
            if (meshDissolveMaterial == null)
            {
                meshDissolveMaterial = skinnedDissolveMaterial;
                Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] Mesh instanced dissolve material not found at '{MeshInstancedDissolveMaterialPath}'. Fallback to default dissolve material.");
            }
            else if (!meshDissolveMaterial.enableInstancing)
            {
                Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] Mesh dissolve material '{meshDissolveMaterial.name}' has GPU Instancing disabled.");
            }

            var dissolveShader = skinnedDissolveMaterial != null ? skinnedDissolveMaterial.shader : ResolveDissolveShader();
            if (dissolveShader == null)
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Dissolve shader not found from '{DissolveMaterialPath}'.");
                return;
            }

            // 지정된 렌더러 데이터 순회 설정 구간
            var configuredCount = 0;
            foreach (var path in RendererDataPaths)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null)
                {
                    Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] RendererData not found at path: {path}");
                    continue;
                }

                ConfigureSingleRendererData(rendererData, dissolveLayerIndex, dissolveShader, skinnedDissolveMaterial, meshDissolveMaterial);
                configuredCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(DeathDissolveSetupEditor)}] Configured {configuredCount} renderer assets.");
        }

        [MenuItem("Tools/Rendering/Death Dissolve/Attach Targets To Active Scene Enemies")]
        public static void AttachTargetsToActiveSceneEnemies()
        {
            // 활성 씬 유효성 가드
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Active scene is invalid.");
                return;
            }

            // 적 레이어 인덱스 탐색 가드
            var enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex < 0)
            {
                Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] Layer 'Enemy' does not exist.");
                return;
            }

            var allHealthComponents = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var updatedCount = 0;

            // 활성 씬 적 대상 컴포넌트 부착/기본값 설정 루프
            foreach (var health in allHealthComponents)
            {
                if (health == null)
                {
                    continue;
                }

                if (health.gameObject.scene != activeScene || health.gameObject.layer != enemyLayerIndex)
                {
                    continue;
                }

                var target = health.GetComponent<CharacterSpecialEffectController>();
                if (target == null)
                {
                    target = Undo.AddComponent<CharacterSpecialEffectController>(health.gameObject);
                }

                Undo.RecordObject(target, "Configure Death Dissolve Target");
                target.ConfigureDefaults(DissolveLayerName, SkinnedDissolveRenderingLayerMask, MeshDissolveRenderingLayerMask);
                EditorUtility.SetDirty(target);
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log($"[{nameof(DeathDissolveSetupEditor)}] Attached or updated {updatedCount} enemy targets in '{activeScene.name}'.");
        }

        private static void ConfigureSingleRendererData(
            UniversalRendererData rendererData,
            int dissolveLayerIndex,
            Shader dissolveShader,
            Material skinnedDissolveMaterial,
            Material meshDissolveMaterial)
        {
            // 디졸브 렌더러 피처 조회 또는 생성 구간
            var feature = rendererData.rendererFeatures.OfType<DeathDissolveRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DeathDissolveRendererFeature>();
                feature.name = nameof(DeathDissolveRendererFeature);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            // 피처 기본값 주입 + 활성화 구간
            var dissolveLayerMask = (LayerMask)(1 << dissolveLayerIndex);
            feature.ConfigureDefaults(
                dissolveLayerMask,
                SkinnedDissolveRenderingLayerMask,
                MeshDissolveRenderingLayerMask,
                dissolveShader,
                skinnedDissolveMaterial,
                meshDissolveMaterial);
            feature.SetActive(true);

            // 기본 Opaque/Transparent 패스 제외 마스크 설정 구간
            var exclusionBit = ~(1 << dissolveLayerIndex);
            var opaqueLayerMask = (int)rendererData.opaqueLayerMask;
            var transparentLayerMask = (int)rendererData.transparentLayerMask;

            rendererData.opaqueLayerMask = opaqueLayerMask & exclusionBit;
            rendererData.transparentLayerMask = transparentLayerMask & exclusionBit;

            InvokeValidateRendererFeatures(rendererData);

            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static Shader ResolveDissolveShader()
        {
            // 머티리얼 참조 우선 셰이더 해석 경로
            var material = AssetDatabase.LoadAssetAtPath<Material>(DissolveMaterialPath);
            if (material != null && material.shader != null)
            {
                return material.shader;
            }

            // 셰이더 이름 직접 탐색 폴백 경로
            return Shader.Find("Shader Graphs/Dissolve/Dissolve_Metallic");
        }

        private static int EnsureLayer(string layerName)
        {
            // 기존 레이어 존재 시 즉시 반환 경로
            var index = LayerMask.NameToLayer(layerName);
            if (index >= 0)
            {
                return index;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");

            // 사용자 레이어 슬롯 탐색 후 신규 할당 경로
            for (var i = 8; i < 32; i++)
            {
                var layerProperty = layersProperty.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layerProperty.stringValue))
                {
                    continue;
                }

                layerProperty.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }

            return -1;
        }

        private static void InvokeValidateRendererFeatures(ScriptableRendererData rendererData)
        {
            // 내부 검증 메서드 리플렉션 호출 구간
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var method = typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", flags);
            method?.Invoke(rendererData, Array.Empty<object>());
        }
    }
}
#endif

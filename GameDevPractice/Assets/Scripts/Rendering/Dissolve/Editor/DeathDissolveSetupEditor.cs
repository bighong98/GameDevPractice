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
    public static class DeathDissolveSetupEditor
    {
        private const string DissolveLayerName = "DissolveOnly";
        private const int DissolveRenderingLayerBitIndex = 3;
        private const uint DissolveRenderingLayerMask = 1u << DissolveRenderingLayerBitIndex;

        private const string DissolveMaterialPath = "Assets/Asset Packs/VFX/ShaderGraph_Dissolve/URP/Materials/Shader Graphs_Dissolve_Dissolve_Metallic.mat";

        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Tools/Rendering/Death Dissolve/Setup All")]
        public static void SetupAll()
        {
            ConfigureUrpRenderers();
            AttachTargetsToActiveSceneEnemies();
        }

        [MenuItem("Tools/Rendering/Death Dissolve/Configure URP Renderers")]
        public static void ConfigureUrpRenderers()
        {
            var dissolveLayerIndex = EnsureLayer(DissolveLayerName);
            if (dissolveLayerIndex < 0)
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Failed to allocate layer '{DissolveLayerName}'.");
                return;
            }

            var dissolveMaterial = AssetDatabase.LoadAssetAtPath<Material>(DissolveMaterialPath);
            var dissolveShader = dissolveMaterial != null ? dissolveMaterial.shader : ResolveDissolveShader();
            if (dissolveShader == null)
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Dissolve shader not found from '{DissolveMaterialPath}'.");
                return;
            }

            var configuredCount = 0;
            foreach (var path in RendererDataPaths)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null)
                {
                    Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] RendererData not found at path: {path}");
                    continue;
                }

                ConfigureSingleRendererData(rendererData, dissolveLayerIndex, dissolveShader, dissolveMaterial);
                configuredCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(DeathDissolveSetupEditor)}] Configured {configuredCount} renderer assets.");
        }

        [MenuItem("Tools/Rendering/Death Dissolve/Attach Targets To Active Scene Enemies")]
        public static void AttachTargetsToActiveSceneEnemies()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError($"[{nameof(DeathDissolveSetupEditor)}] Active scene is invalid.");
                return;
            }

            var enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex < 0)
            {
                Debug.LogWarning($"[{nameof(DeathDissolveSetupEditor)}] Layer 'Enemy' does not exist.");
                return;
            }

            var allHealthComponents = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var updatedCount = 0;

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
                target.ConfigureDefaults(DissolveLayerName, DissolveRenderingLayerMask);
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
            Material dissolveMaterial)
        {
            var feature = rendererData.rendererFeatures.OfType<DeathDissolveRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DeathDissolveRendererFeature>();
                feature.name = nameof(DeathDissolveRendererFeature);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            var dissolveLayerMask = (LayerMask)(1 << dissolveLayerIndex);
            feature.ConfigureDefaults(dissolveLayerMask, DissolveRenderingLayerMask, dissolveShader, dissolveMaterial);
            feature.SetActive(true);

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
            var material = AssetDatabase.LoadAssetAtPath<Material>(DissolveMaterialPath);
            if (material != null && material.shader != null)
            {
                return material.shader;
            }

            return Shader.Find("Shader Graphs/Dissolve/Dissolve_Metallic");
        }

        private static int EnsureLayer(string layerName)
        {
            var index = LayerMask.NameToLayer(layerName);
            if (index >= 0)
            {
                return index;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");

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
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var method = typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", flags);
            method?.Invoke(rendererData, Array.Empty<object>());
        }
    }
}
#endif

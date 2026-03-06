using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEngine;
using UnityEngine.UI;

namespace TH.Resource.Editor
{
    public static class LoadingSceneResourcesIsolationEditor
    {
        private const string MenuPath = "Tools/Addressables/LoadingScene/Clone Duplicates To Resources";
        private const string TargetFolder = "Assets/Resources/LoadingScene";
        private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
        private const string LoadingSceneUiPrefabPath = "Assets/Game/UI/Scene/LoadingSceneUI.prefab";
        private const string SharedGroupName = "Shared";
        private const string AnalyzeSnapshotMenuPath = "Tools/Addressables/Analyze/Run Duplicate Checks And Export Snapshot";
        private const string AnalyzeSnapshotFolder = "AddressablesAnalyze";
        private const string AnalyzeSnapshotBaseName = "AddressablesAnalyseResults";
        private const string LoadingSceneWhiteTexturePath = "Assets/Resources/LoadingScene/LoadingSceneWhite.png";
        private const string GlowCirclePointPrefabPath = "Assets/Game/UI/Feedback/GlowCirclePointUI.prefab";
        private const string GlowSparklePointPrefabPath = "Assets/Game/UI/Feedback/GlowSparklePointUI.prefab";

        private static readonly string[] PackageFallbackShaderPaths =
        {
            "Packages/com.unity.render-pipelines.universal/Shaders/Utils/FallbackError.shader",
            "Packages/com.unity.render-pipelines.core/Runtime/RenderPipelineResources/FallbackShader.shader"
        };

        private static readonly string[] SourceAssetPaths =
        {
            "Assets/Game/TMP/AddressableSplit/TMP_SDF-Mobile.shader",
            "Assets/TextMesh Pro/Shaders/TMP_SDF.shader",
            "Assets/TextMesh Pro/Shaders/TMPro.cginc",
            "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc",
            "Assets/TextMesh Pro/Fonts/Maplestory Light.ttf",
            "Assets/TextMesh Pro/Fonts/Maplestory Light SDF.asset",
            "Assets/TextMesh Pro/Fonts/Maplestory Bold.ttf",
            "Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset",
            "Assets/Asset Packs/Skyden_Games/Free_Casual_GUI/Demo/Sprites/Others/Shapes/Shape_16.png"
        };

        [MenuItem(MenuPath)]
        private static void CloneDuplicatesToResources()
        {
            EnsureFolder(TargetFolder);

            var guidMap = new Dictionary<string, string>(StringComparer.Ordinal);
            int copyCreated = 0;
            int copyReused = 0;

            for (int i = 0; i < SourceAssetPaths.Length; i++)
            {
                string sourcePath = SourceAssetPaths[i];
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Missing source: {sourcePath}");
                    continue;
                }

                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                if (string.IsNullOrEmpty(sourceGuid))
                {
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Source guid missing: {sourcePath}");
                    continue;
                }

                string targetPath = Path.Combine(TargetFolder, Path.GetFileName(sourcePath)).Replace("\\", "/");
                if (!File.Exists(targetPath))
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                    {
                        Debug.LogError($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Copy failed: {sourcePath} -> {targetPath}");
                        continue;
                    }

                    copyCreated++;
                }
                else
                {
                    copyReused++;
                }

                string targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
                if (string.IsNullOrEmpty(targetGuid))
                {
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Target guid missing: {targetPath}");
                    continue;
                }

                guidMap[sourceGuid] = targetGuid;
            }

            int cloneRefRetargetCount = RetargetGuidsInFolderTextAssets(TargetFolder, guidMap);
            int prefabRetargetCount = RetargetGuidsInTextAsset(LoadingSceneUiPrefabPath, guidMap);
            int sceneRetargetCount = RetargetGuidsInTextAsset(LoadingScenePath, guidMap);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(LoadingSceneResourcesIsolationEditor)}] Completed. " +
                $"copyCreated={copyCreated}, copyReused={copyReused}, " +
                $"cloneRefRetargetCount={cloneRefRetargetCount}, " +
                $"prefabRetargetCount={prefabRetargetCount}, sceneRetargetCount={sceneRetargetCount}");
        }

        [MenuItem("Tools/Addressables/LoadingScene/Register Package Fallback Shaders To Shared")]
        private static void RegisterPackageFallbackShadersToShared()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Addressable settings not found");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(SharedGroupName);
            if (group == null)
            {
                Debug.LogError($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Group not found: {SharedGroupName}");
                return;
            }

            int added = 0;
            int moved = 0;
            int skipped = 0;

            foreach (string assetPath in PackageFallbackShaderPaths)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                if (shader == null)
                {
                    skipped++;
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Package shader not found: {assetPath}");
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    skipped++;
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Package shader guid missing: {assetPath}");
                    continue;
                }

                AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null && existingEntry.parentGroup == group)
                {
                    continue;
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
                if (entry == null)
                {
                    skipped++;
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Addressable registration failed: {assetPath}");
                    continue;
                }

                entry.SetAddress(Path.GetFileNameWithoutExtension(assetPath), false);

                if (existingEntry == null)
                {
                    added++;
                }
                else
                {
                    moved++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(LoadingSceneResourcesIsolationEditor)}] RegisterPackageFallbackShadersToShared completed. " +
                $"added={added}, moved={moved}, skipped={skipped}");
        }

        [MenuItem(AnalyzeSnapshotMenuPath)]
        private static void RunDuplicateChecksAndExportSnapshot()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Addressable settings not found");
                return;
            }

            string snapshotPath = GetNextAnalyzeSnapshotPath();
            var export = new AnalyzeSnapshotExport(new List<AnalyzeSnapshotRuleResult>
            {
                RunAnalyzeRule(settings, new CheckSceneDupeDependencies()),
                CreateEmptyAnalyzeRuleResult("Bundle Layout Preview"),
                RunAnalyzeRule(settings, new CheckBundleDupeDependencies()),
                RunAnalyzeRule(settings, new CheckResourcesDupeDependencies()),
                CreateEmptyAnalyzeRuleResult(typeof(AnalyzeRule).FullName)
            });

            File.WriteAllText(snapshotPath, JsonUtility.ToJson(export));
            AssetDatabase.Refresh();

            Debug.Log($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Analyze snapshot exported: {snapshotPath}");
        }

        private static AnalyzeSnapshotRuleResult RunAnalyzeRule(AddressableAssetSettings settings, AnalyzeRule rule)
        {
            List<AnalyzeRule.AnalyzeResult> results = rule.RefreshAnalysis(settings);
            if (results == null)
            {
                results = new List<AnalyzeRule.AnalyzeResult>();
            }

            return new AnalyzeSnapshotRuleResult(rule.ruleName, results);
        }

        private static AnalyzeSnapshotRuleResult CreateEmptyAnalyzeRuleResult(string ruleName)
        {
            return new AnalyzeSnapshotRuleResult(ruleName, new List<AnalyzeRule.AnalyzeResult>());
        }

        private static string GetNextAnalyzeSnapshotPath()
        {
            if (!Directory.Exists(AnalyzeSnapshotFolder))
            {
                Directory.CreateDirectory(AnalyzeSnapshotFolder);
            }

            int maxVersion = 0;
            string[] existingFiles = Directory.GetFiles(AnalyzeSnapshotFolder, AnalyzeSnapshotBaseName + "*.json");
            for (int i = 0; i < existingFiles.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(existingFiles[i]);
                string prefix = AnalyzeSnapshotBaseName + "_";
                if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string suffix = fileName.Substring(prefix.Length);
                if (int.TryParse(suffix, out int version) && version > maxVersion)
                {
                    maxVersion = version;
                }
            }

            string nextFileName = $"{AnalyzeSnapshotBaseName}_{maxVersion + 1}.json";
            return Path.Combine(AnalyzeSnapshotFolder, nextFileName);
        }

        [MenuItem("Tools/Addressables/LoadingScene/Assign Explicit White Texture To Glow Prefabs")]
        private static void AssignExplicitWhiteTextureToGlowPrefabs()
        {
            EnsureFolder(TargetFolder);

            Texture2D texture = EnsureLoadingSceneWhiteTexture();
            if (texture == null)
            {
                Debug.LogError($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Failed to create or load texture: {LoadingSceneWhiteTexturePath}");
                return;
            }

            int updated = 0;
            if (AssignTextureToRawImagePrefab(GlowCirclePointPrefabPath, texture))
            {
                updated++;
            }

            if (AssignTextureToRawImagePrefab(GlowSparklePointPrefabPath, texture))
            {
                updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(LoadingSceneResourcesIsolationEditor)}] AssignExplicitWhiteTextureToGlowPrefabs completed. " +
                $"updated={updated}, texturePath={LoadingSceneWhiteTexturePath}");
        }

        private static Texture2D EnsureLoadingSceneWhiteTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(LoadingSceneWhiteTexturePath);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            byte[] pngBytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return null;
            }

            File.WriteAllBytes(LoadingSceneWhiteTexturePath, pngBytes);
            AssetDatabase.ImportAsset(LoadingSceneWhiteTexturePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LoadingSceneWhiteTexturePath);
        }

        private static bool AssignTextureToRawImagePrefab(string prefabPath, Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] Failed to load prefab: {prefabPath}");
                return false;
            }

            try
            {
                RawImage rawImage = prefabRoot.GetComponent<RawImage>();
                if (rawImage == null)
                {
                    rawImage = prefabRoot.GetComponentInChildren<RawImage>(includeInactive: true);
                }

                if (rawImage == null)
                {
                    Debug.LogWarning($"[{nameof(LoadingSceneResourcesIsolationEditor)}] RawImage not found: {prefabPath}");
                    return false;
                }

                if (rawImage.texture == texture)
                {
                    return false;
                }

                rawImage.texture = texture;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static int RetargetGuidsInFolderTextAssets(string folderPath, Dictionary<string, string> guidMap)
        {
            if (guidMap.Count == 0)
            {
                return 0;
            }

            int total = 0;
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsTextSerializableAssetPath(path))
                {
                    continue;
                }

                total += RetargetGuidsInTextAsset(path, guidMap);
            }

            return total;
        }

        private static int RetargetGuidsInTextAsset(string assetPath, Dictionary<string, string> guidMap)
        {
            if (guidMap.Count == 0 || !File.Exists(assetPath))
            {
                return 0;
            }

            string original = File.ReadAllText(assetPath);
            string updated = original;
            int replaceCount = 0;

            foreach (KeyValuePair<string, string> pair in guidMap)
            {
                int beforeLen = updated.Length;
                updated = updated.Replace(pair.Key, pair.Value);
                if (!ReferenceEquals(updated, null) && updated.Length == beforeLen && !original.Contains(pair.Key))
                {
                    continue;
                }
            }

            if (updated == original)
            {
                return 0;
            }

            foreach (KeyValuePair<string, string> pair in guidMap)
            {
                replaceCount += CountOccurrences(original, pair.Key) - CountOccurrences(updated, pair.Key);
            }

            File.WriteAllText(assetPath, updated);
            return Math.Max(replaceCount, 1);
        }

        private static bool IsTextSerializableAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Directory.Exists(path))
            {
                return false;
            }

            string ext = Path.GetExtension(path);
            return ext.Equals(".asset", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".unity", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".mat", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".shader", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountOccurrences(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            int startIndex = 0;
            while (true)
            {
                int idx = text.IndexOf(value, startIndex, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                count++;
                startIndex = idx + value.Length;
            }

            return count;
        }

        [Serializable]
        private sealed class AnalyzeSnapshotExport
        {
            [SerializeField] private List<AnalyzeSnapshotRuleResult> m_RuleToResults;

            public AnalyzeSnapshotExport(List<AnalyzeSnapshotRuleResult> ruleToResults)
            {
                m_RuleToResults = ruleToResults;
            }
        }

        [Serializable]
        private sealed class AnalyzeSnapshotRuleResult
        {
            [SerializeField] public string RuleName;
            [SerializeField] public List<AnalyzeRule.AnalyzeResult> Results;

            public AnalyzeSnapshotRuleResult(string ruleName, List<AnalyzeRule.AnalyzeResult> results)
            {
                RuleName = ruleName;
                Results = results;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException($"Invalid project folder path: {folderPath}");
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}

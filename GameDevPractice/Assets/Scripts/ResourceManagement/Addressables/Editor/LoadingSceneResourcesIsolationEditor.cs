using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TH.Resource.Editor
{
    public static class LoadingSceneResourcesIsolationEditor
    {
        private const string MenuPath = "Tools/Addressables/LoadingScene/Clone Duplicates To Resources";
        private const string TargetFolder = "Assets/Resources/LoadingScene";
        private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
        private const string LoadingSceneUiPrefabPath = "Assets/Game/UI/Scene/LoadingSceneUI.prefab";

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

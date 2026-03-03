using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TH.Resource.Editor
{
    public static class TmpAddressableIsolationEditor
    {
        private const string MenuRoot = "Tools/Addressables/TMP Isolation/";
        private const string TargetFolder = "Assets/Game/TMP/AddressableSplit";
        private const string TargetGroup = "Shared";
        private const string TmpExamplesResourcesPath = "Assets/TextMesh Pro/Examples & Extras/Resources";
        private const string TmpExamplesResourcesDisabledPath = "Assets/TextMesh Pro/Examples & Extras/Resources_DISABLED";


        private static readonly string[] SupportShaderIncludePaths =
        {
            "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc",
            "Assets/TextMesh Pro/Shaders/TMPro.cginc"
        };

        private static readonly string[] SourceAssetPaths =
        {
            "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader",
            "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset",
            "Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat"
        };

                [MenuItem(MenuRoot + "Disable TMP Examples Resources")]
        private static void DisableTmpExamplesResources()
        {
            if (!AssetDatabase.IsValidFolder(TmpExamplesResourcesPath))
            {
                Debug.LogWarning($"[{nameof(TmpAddressableIsolationEditor)}] Source folder not found: {TmpExamplesResourcesPath}");
                return;
            }

            if (AssetDatabase.IsValidFolder(TmpExamplesResourcesDisabledPath))
            {
                Debug.Log($"[{nameof(TmpAddressableIsolationEditor)}] Already disabled: {TmpExamplesResourcesDisabledPath}");
                return;
            }

            string error = AssetDatabase.MoveAsset(TmpExamplesResourcesPath, TmpExamplesResourcesDisabledPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[{nameof(TmpAddressableIsolationEditor)}] MoveAsset failed: {error}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[{nameof(TmpAddressableIsolationEditor)}] Moved folder: {TmpExamplesResourcesPath} -> {TmpExamplesResourcesDisabledPath}");
        }

[MenuItem(MenuRoot + "Apply Split To Shared")]
        private static void ApplySplitToShared()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(TmpAddressableIsolationEditor)}] Addressable settings not found");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(TargetGroup);
            if (group == null)
            {
                Debug.LogError($"[{nameof(TmpAddressableIsolationEditor)}] Group not found: {TargetGroup}");
                return;
            }

            EnsureFolder(TargetFolder);

            var sourceToCloneMap = new Dictionary<string, Object>(StringComparer.Ordinal);
            var clonePaths = new List<string>();
            int copyCreated = 0;
            int copyReused = 0;

            foreach (string sourcePath in SourceAssetPaths)
            {
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[{nameof(TmpAddressableIsolationEditor)}] Missing source asset: {sourcePath}");
                    continue;
                }

                string clonePath = Path.Combine(TargetFolder, Path.GetFileName(sourcePath)).Replace("\\", "/");
                if (!File.Exists(clonePath))
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, clonePath))
                    {
                        Debug.LogError($"[{nameof(TmpAddressableIsolationEditor)}] Failed to copy asset: {sourcePath} -> {clonePath}");
                        continue;
                    }

                    copyCreated++;
                }
                else
                {
                    copyReused++;
                }

                if (!BuildSourceCloneReferenceMap(sourcePath, clonePath, sourceToCloneMap))
                {
                    Debug.LogWarning($"[{nameof(TmpAddressableIsolationEditor)}] Source/clone map build failed: {sourcePath}");
                    continue;
                }

                clonePaths.Add(clonePath);
            }

            int cloneSelfRetargetAssets = 0;
            int cloneSelfRetargetRefs = 0;
            RetargetFolderAssets(TargetFolder, sourceToCloneMap, ref cloneSelfRetargetAssets, ref cloneSelfRetargetRefs);

            int addrAssetRetargetAssets = 0;
            int addrAssetRetargetRefs = 0;
            RetargetAddressableEntryAssets(settings, sourceToCloneMap, ref addrAssetRetargetAssets, ref addrAssetRetargetRefs);

            int addedToShared = 0;
            foreach (string clonePath in clonePaths)
            {
                string guid = AssetDatabase.AssetPathToGUID(clonePath);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                AddressableAssetEntry entry = settings.FindAssetEntry(guid);
                if (entry != null && entry.parentGroup == group)
                {
                    continue;
                }

                settings.CreateOrMoveEntry(guid, group);
                addedToShared++;
            }

            int removedSourceEntries = RemoveSourceEntries(settings, SourceAssetPaths);
            int supportIncludesCopied = CopySupportFiles(SupportShaderIncludePaths, TargetFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(TmpAddressableIsolationEditor)}] ApplySplitToShared finished. " +
                $"copyCreated={copyCreated}, copyReused={copyReused}, " +
                $"cloneSelfRetargetAssets={cloneSelfRetargetAssets}, cloneSelfRetargetRefs={cloneSelfRetargetRefs}, " +
                $"addressableRetargetAssets={addrAssetRetargetAssets}, addressableRetargetRefs={addrAssetRetargetRefs}, " +
                $"addedToShared={addedToShared}, removedSourceEntries={removedSourceEntries}, supportIncludesCopied={supportIncludesCopied}");
        }

        private static int CopySupportFiles(IReadOnlyList<string> supportPaths, string targetFolder)
        {
            if (supportPaths == null || supportPaths.Count == 0)
            {
                return 0;
            }

            int copied = 0;
            for (int i = 0; i < supportPaths.Count; i++)
            {
                string sourcePath = supportPaths[i];
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                string targetPath = Path.Combine(targetFolder, Path.GetFileName(sourcePath)).Replace("\\", "/");
                if (!File.Exists(targetPath))
                {
                    if (AssetDatabase.CopyAsset(sourcePath, targetPath))
                    {
                        copied++;
                    }
                }
            }

            return copied;
        }

        private static void RetargetFolderAssets(string folderPath, Dictionary<string, Object> sourceToCloneMap, ref int changedAssetCount, ref int changedRefCount)
        {
            if (sourceToCloneMap.Count == 0)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || Directory.Exists(assetPath))
                {
                    continue;
                }

                RetargetAssetAtPath(assetPath, sourceToCloneMap, ref changedAssetCount, ref changedRefCount);
            }
        }

        private static void RetargetAddressableEntryAssets(AddressableAssetSettings settings, Dictionary<string, Object> sourceToCloneMap, ref int changedAssetCount, ref int changedRefCount)
        {
            if (sourceToCloneMap.Count == 0)
            {
                return;
            }

            var visitedPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    string entryPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(entryPath))
                    {
                        continue;
                    }

                    if (Directory.Exists(entryPath))
                    {
                        string[] childGuids = AssetDatabase.FindAssets(string.Empty, new[] { entryPath });
                        foreach (string childGuid in childGuids)
                        {
                            string childPath = AssetDatabase.GUIDToAssetPath(childGuid);
                            if (ShouldSkipRetargetPath(childPath))
                            {
                                continue;
                            }

                            if (!visitedPaths.Add(childPath))
                            {
                                continue;
                            }

                            RetargetAssetAtPath(childPath, sourceToCloneMap, ref changedAssetCount, ref changedRefCount);
                        }

                        continue;
                    }

                    if (ShouldSkipRetargetPath(entryPath))
                    {
                        continue;
                    }

                    if (!visitedPaths.Add(entryPath))
                    {
                        continue;
                    }

                    RetargetAssetAtPath(entryPath, sourceToCloneMap, ref changedAssetCount, ref changedRefCount);
                }
            }
        }

        private static bool ShouldSkipRetargetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            if (Directory.Exists(assetPath))
            {
                return true;
            }

            if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (assetPath.StartsWith(TargetFolder, StringComparison.Ordinal))
            {
                return true;
            }

            if (assetPath.Contains("/Resources/", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static void RetargetAssetAtPath(string assetPath, Dictionary<string, Object> sourceToCloneMap, ref int changedAssetCount, ref int changedRefCount)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            bool pathChanged = false;

            for (int i = 0; i < assets.Length; i++)
            {
                Object obj = assets[i];
                if (obj == null)
                {
                    continue;
                }

                var serializedObject = new SerializedObject(obj);
                SerializedProperty iterator = serializedObject.GetIterator();
                bool objectChanged = false;
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object referenced = iterator.objectReferenceValue;
                    if (referenced == null)
                    {
                        continue;
                    }

                    if (!TryGetGuidAndLocalId(referenced, out string sourceGuid, out long sourceLocalId))
                    {
                        continue;
                    }

                    string sourceObjectKey = MakeObjectKey(sourceGuid, sourceLocalId);
                    if (!sourceToCloneMap.TryGetValue(sourceObjectKey, out Object cloneReference) || cloneReference == null)
                    {
                        continue;
                    }

                    if (ReferenceEquals(referenced, cloneReference))
                    {
                        continue;
                    }

                    iterator.objectReferenceValue = cloneReference;
                    objectChanged = true;
                    changedRefCount++;
                }

                if (!objectChanged)
                {
                    continue;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                pathChanged = true;
            }

            if (pathChanged)
            {
                changedAssetCount++;
            }
        }

        private static bool BuildSourceCloneReferenceMap(string sourcePath, string clonePath, Dictionary<string, Object> sourceToCloneMap)
        {
            Object[] sourceAssets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
            Object[] cloneAssets = AssetDatabase.LoadAllAssetsAtPath(clonePath);
            if (sourceAssets == null || cloneAssets == null || sourceAssets.Length == 0 || cloneAssets.Length == 0)
            {
                return false;
            }

            var cloneByLocalId = new Dictionary<long, Object>();
            for (int i = 0; i < cloneAssets.Length; i++)
            {
                Object cloneAsset = cloneAssets[i];
                if (cloneAsset == null)
                {
                    continue;
                }

                if (!TryGetGuidAndLocalId(cloneAsset, out _, out long cloneLocalId))
                {
                    continue;
                }

                cloneByLocalId[cloneLocalId] = cloneAsset;
            }

            bool mappedAny = false;
            for (int i = 0; i < sourceAssets.Length; i++)
            {
                Object sourceAsset = sourceAssets[i];
                if (sourceAsset == null)
                {
                    continue;
                }

                if (!TryGetGuidAndLocalId(sourceAsset, out string sourceGuid, out long sourceLocalId))
                {
                    continue;
                }

                if (!cloneByLocalId.TryGetValue(sourceLocalId, out Object cloneReference) || cloneReference == null)
                {
                    continue;
                }

                sourceToCloneMap[MakeObjectKey(sourceGuid, sourceLocalId)] = cloneReference;
                mappedAny = true;
            }

            return mappedAny;
        }

        private static int RemoveSourceEntries(AddressableAssetSettings settings, IReadOnlyList<string> sourcePaths)
        {
            int removedCount = 0;
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string sourcePath = sourcePaths[i];
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                if (string.IsNullOrEmpty(sourceGuid))
                {
                    continue;
                }

                AddressableAssetEntry sourceEntry = settings.FindAssetEntry(sourceGuid);
                if (sourceEntry == null)
                {
                    continue;
                }

                if (settings.RemoveAssetEntry(sourceGuid))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        private static bool TryGetGuidAndLocalId(Object obj, out string guid, out long localId)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId);
        }

        private static string MakeObjectKey(string guid, long localId)
        {
            return guid + ":" + localId;
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

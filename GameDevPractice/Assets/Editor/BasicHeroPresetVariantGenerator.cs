#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class BasicHeroPresetVariantGenerator
{
    private const string SourceFolderAddressKey = "Outfit Source Presets";
    private const string SourceFolderWithHeadwearAddressKey = "Outfit Source Presets_wHeadwear";
    private const string OutputFolderAddressKey = "Outfit Output Presets";
    private const string OutputFolderWithHeadwearAddressKey = "Outfit Output Presets_wHeadwear";

    private const string FemaleTemplateAddressKey = "BasicHero_F Variant Template";
    private const string MaleTemplateAddressKey = "BasicHero_M Variant Template";

    [MenuItem("Tools/Outfit/Generate Preset Variants (BasicHero)")]
    private static void GeneratePresetVariants()
    {
        GeneratePresetVariants(SourceFolderAddressKey, OutputFolderAddressKey, "Presets");
    }

    [MenuItem("Tools/Outfit/Generate Preset Variants (BasicHero, wHeadwear)")]
    private static void GeneratePresetVariantsWithHeadwear()
    {
        GeneratePresetVariants(SourceFolderWithHeadwearAddressKey, OutputFolderWithHeadwearAddressKey, "Presets_wHeadwear");
    }

    [MenuItem("Tools/Outfit/Generate Preset Variants (BasicHero, All)")]
    private static void GenerateAllPresetVariants()
    {
        GeneratePresetVariants();
        GeneratePresetVariantsWithHeadwear();
    }

    private static void GeneratePresetVariants(string sourceFolderAddressKey, string outputFolderAddressKey, string setName)
    {
        var sourceFolder = ResolvePathByAddressKey(sourceFolderAddressKey);
        var outputFolder = ResolvePathByAddressKey(outputFolderAddressKey);
        if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(outputFolder))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(sourceFolder))
        {
            Debug.LogError($"[BasicHeroPresetVariantGenerator] Source folder not found ({setName}): {sourceFolder}");
            return;
        }

        EnsureFolderPath(outputFolder);

        var femaleTemplatePath = ResolvePathByAddressKey(FemaleTemplateAddressKey);
        var maleTemplatePath = ResolvePathByAddressKey(MaleTemplateAddressKey);
        if (string.IsNullOrEmpty(femaleTemplatePath) || string.IsNullOrEmpty(maleTemplatePath))
        {
            return;
        }

        var femaleTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(femaleTemplatePath);
        var maleTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(maleTemplatePath);

        if (femaleTemplate == null)
        {
            Debug.LogError($"[BasicHeroPresetVariantGenerator] Female template not found: {femaleTemplatePath}");
            return;
        }

        if (maleTemplate == null)
        {
            Debug.LogError($"[BasicHeroPresetVariantGenerator] Male template not found: {maleTemplatePath}");
            return;
        }

        var sourceGuids = AssetDatabase.FindAssets("t:Prefab", new[] { sourceFolder });
        Array.Sort(sourceGuids, CompareGuidsByPath);

        var settings = new PrefabReplacingSettings
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
            changeRootNameToAssetName = false,
            logInfo = false
        };

        var succeeded = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var guid in sourceGuids)
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            var sourceName = Path.GetFileName(sourcePath);
            var template = SelectTemplateFromName(sourceName, femaleTemplate, maleTemplate);

            if (template == null)
            {
                Debug.LogWarning($"[BasicHeroPresetVariantGenerator] Skipped (cannot detect gender): {sourcePath}");
                skipped++;
                continue;
            }

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[BasicHeroPresetVariantGenerator] Failed to load source prefab: {sourcePath}");
                failed++;
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[BasicHeroPresetVariantGenerator] Failed to instantiate source prefab: {sourcePath}");
                failed++;
                continue;
            }

            try
            {
                var sourceActiveStates = CaptureActiveStates(instance.transform);

                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(instance, template, settings, InteractionMode.AutomatedAction);

                ApplyActiveStates(instance.transform, sourceActiveStates, out var appliedActiveCount, out var missingActivePathCount, out var comparedActiveCount);

                var outputPath = $"{outputFolder}/{sourceName}";
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                if (saved == null)
                {
                    Debug.LogError($"[BasicHeroPresetVariantGenerator] Failed to save output prefab: {outputPath}");
                    failed++;
                    continue;
                }

                succeeded++;

                if (missingActivePathCount > 0)
                {
                    Debug.LogWarning($"[BasicHeroPresetVariantGenerator] Generated with active-state path misses ({missingActivePathCount}): {outputPath}");
                }

                Debug.Log($"[BasicHeroPresetVariantGenerator] Generated ({setName}): {outputPath} (activeApplied={appliedActiveCount}, activeCompared={comparedActiveCount}, activeMissing={missingActivePathCount})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BasicHeroPresetVariantGenerator] Failed for {sourcePath}\n{ex}");
                failed++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BasicHeroPresetVariantGenerator] Done ({setName}). Succeeded={succeeded}, Skipped={skipped}, Failed={failed}");
    }

    private static GameObject SelectTemplateFromName(string fileName, GameObject femaleTemplate, GameObject maleTemplate)
    {
        if (fileName.IndexOf("BasicHero_F_", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return femaleTemplate;
        }

        if (fileName.IndexOf("BasicHero_M_", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return maleTemplate;
        }

        return null;
    }

    private static Dictionary<string, bool> CaptureActiveStates(Transform root)
    {
        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        CaptureActiveStatesRecursive(root, string.Empty, states);
        return states;
    }

    private static void CaptureActiveStatesRecursive(Transform current, string path, Dictionary<string, bool> states)
    {
        states[path] = current.gameObject.activeSelf;

        for (var i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            var childPath = string.IsNullOrEmpty(path) ? child.name : $"{path}/{child.name}";
            CaptureActiveStatesRecursive(child, childPath, states);
        }
    }

    private static void ApplyActiveStates(Transform targetRoot, Dictionary<string, bool> sourceStates, out int appliedCount, out int missingPathCount, out int comparedCount)
    {
        appliedCount = 0;
        missingPathCount = 0;
        comparedCount = 0;

        foreach (var pair in sourceStates)
        {
            var target = FindByRelativePath(targetRoot, pair.Key);
            if (target == null)
            {
                missingPathCount++;
                continue;
            }

            comparedCount++;

            if (target.gameObject.activeSelf == pair.Value)
            {
                continue;
            }

            target.gameObject.SetActive(pair.Value);
            appliedCount++;
        }
    }

    private static Transform FindByRelativePath(Transform root, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return root;
        }

        return root.Find(relativePath);
    }

    private static int CompareGuidsByPath(string a, string b)
    {
        var pathA = AssetDatabase.GUIDToAssetPath(a);
        var pathB = AssetDatabase.GUIDToAssetPath(b);
        return string.Compare(pathA, pathB, StringComparison.Ordinal);
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new ArgumentException($"Folder must start with 'Assets': {folderPath}");
        }

        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return null;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError($"[BasicHeroPresetVariantGenerator] Addressable settings not found. key={addressKey}");
            return null;
        }

        for (int g = 0; g < settings.groups.Count; g++)
        {
            var group = settings.groups[g];
            if (group == null)
                continue;

            foreach (var entry in group.entries)
            {
                if (entry == null)
                    continue;

                if (!string.Equals(entry.address, addressKey, StringComparison.Ordinal))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
        }

        Debug.LogError($"[BasicHeroPresetVariantGenerator] Addressable key not found: {addressKey}");
        return null;
    }
}
#endif

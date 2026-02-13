#if UNITY_EDITOR
using System;
using System.IO;
using TH.Item;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class OutfitTemplateSetupTool
{
    private const string MaleTemplatePrefabAddressKey = "outfit.template.male";
    private const string FemaleTemplatePrefabAddressKey = "outfit.template.female";
    private const string OutfitDataFolderAddressKey = "outfit.data.folder";
    private static readonly string[] TemplatePrefabAddressKeys =
    {
        MaleTemplatePrefabAddressKey,
        FemaleTemplatePrefabAddressKey
    };

    [MenuItem("Tools/Outfit/Setup BasicHero Variant Templates")]
    private static void Setup()
    {
        var outfitDataRootPath = ResolvePathByAddressKey(OutfitDataFolderAddressKey);
        if (string.IsNullOrEmpty(outfitDataRootPath))
            return;

        var templatePrefabPaths = ResolveTemplatePrefabPaths();
        if (templatePrefabPaths.Count == 0)
            return;

        EnsureFolder(outfitDataRootPath);

        var configuredKeys = new System.Collections.Generic.HashSet<OutfitKeySO>();
        int createdAssets = 0;
        int updatedAssets = 0;
        int addedTags = 0;
        int configuredTags = 0;
        int configuredEmptyTopBottomTags = 0;

        for (int i = 0; i < templatePrefabPaths.Count; i++)
        {
            SetupTemplate(
                templatePrefabPaths[i],
                outfitDataRootPath,
                configuredKeys,
                ref createdAssets,
                ref updatedAssets,
                ref addedTags,
                ref configuredTags,
                ref configuredEmptyTopBottomTags);
        }

        EnsureOutfitKeysAddressable(configuredKeys);
        RestoreLegacyOutfitKeyGroup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[OutfitTemplateSetupTool] Completed. " +
            $"Templates={templatePrefabPaths.Count}, CreatedAssets={createdAssets}, UpdatedAssets={updatedAssets}, AddedTags={addedTags}, ConfiguredTags={configuredTags}, ConfiguredEmptyTopBottomTags={configuredEmptyTopBottomTags}");
    }

    [MenuItem("Tools/Outfit/Check Empty OutfitPartKeyTag In Templates")]
    private static void CheckEmptyOutfitPartKeyTagInTemplate()
    {
        var templatePrefabPaths = ResolveTemplatePrefabPaths();
        if (templatePrefabPaths.Count == 0)
            return;

        int totalCount = 0;
        for (int i = 0; i < templatePrefabPaths.Count; i++)
        {
            var count = CountEmptyTagsInTemplate(templatePrefabPaths[i]);
            totalCount += count;
            Debug.Log($"[OutfitTemplateSetupTool] Empty OutfitPartKeyTag count ({Path.GetFileNameWithoutExtension(templatePrefabPaths[i])}): {count}");
        }

        Debug.Log($"[OutfitTemplateSetupTool] Empty OutfitPartKeyTag total count: {totalCount}");
    }

    [MenuItem("Tools/Outfit/Remove Unsupported Empty OutfitPartKeyTag In Templates")]
    private static void RemoveUnsupportedEmptyOutfitPartKeyTagInTemplate()
    {
        var templatePrefabPaths = ResolveTemplatePrefabPaths();
        if (templatePrefabPaths.Count == 0)
            return;

        int removedCount = 0;
        for (int i = 0; i < templatePrefabPaths.Count; i++)
        {
            removedCount += RemoveUnsupportedEmptyTagsInTemplate(templatePrefabPaths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[OutfitTemplateSetupTool] Removed unsupported empty OutfitPartKeyTag. Templates={templatePrefabPaths.Count}, Removed={removedCount}");
    }

    [MenuItem("Tools/Outfit/Assign OutfitKeySO To Empty OutfitPartKeyTag In Templates")]
    private static void AssignOutfitKeySOToEmptyOutfitPartKeyTagInTemplate()
    {
        var outfitDataRootPath = ResolvePathByAddressKey(OutfitDataFolderAddressKey);
        if (string.IsNullOrEmpty(outfitDataRootPath))
            return;

        var templatePrefabPaths = ResolveTemplatePrefabPaths();
        if (templatePrefabPaths.Count == 0)
            return;

        EnsureFolder(outfitDataRootPath);

        var configuredKeys = new System.Collections.Generic.HashSet<OutfitKeySO>();
        int createdAssets = 0;
        int updatedAssets = 0;
        int assignedCount = 0;
        int skippedNoSlot = 0;

        for (int i = 0; i < templatePrefabPaths.Count; i++)
        {
            AssignOutfitKeysToEmptyTagsInTemplate(
                templatePrefabPaths[i],
                outfitDataRootPath,
                configuredKeys,
                ref createdAssets,
                ref updatedAssets,
                ref assignedCount,
                ref skippedNoSlot);
        }

        EnsureOutfitKeysAddressable(configuredKeys);
        RestoreLegacyOutfitKeyGroup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[OutfitTemplateSetupTool] Assigned OutfitKeySO to empty tags by slot. Templates={templatePrefabPaths.Count}, Assigned={assignedCount}, SkippedNoSlot={skippedNoSlot}, CreatedAssets={createdAssets}, UpdatedAssets={updatedAssets}");
    }

    private static void SetupTemplate(
        string templatePrefabPath,
        string outfitDataRootPath,
        System.Collections.Generic.HashSet<OutfitKeySO> configuredKeys,
        ref int createdAssets,
        ref int updatedAssets,
        ref int addedTags,
        ref int configuredTags,
        ref int configuredEmptyTopBottomTags)
    {
        var root = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        try
        {
            // Include: assign OutfitKeySO for empty tags when they are default supported types(_Top/_Bottom).
            var emptyTags = FindEmptyOutfitPartTags(root);
            for (int i = 0; i < emptyTags.Count; i++)
            {
                var tag = emptyTags[i];
                if (tag == null)
                    continue;

                if (!OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(tag.gameObject.name, out var slot))
                    continue;

                var key = GetOrCreateOutfitKeyAsset(outfitDataRootPath, tag.gameObject.name, slot, ref createdAssets, ref updatedAssets);

                tag.SetOutfitKeyForEditor(key);
                configuredKeys.Add(key);
                configuredEmptyTopBottomTags++;
            }

            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var smr = renderers[i];
                if (smr == null)
                    continue;

                if (!OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(smr.name, out var slot))
                    continue;

                var tag = smr.GetComponent<OutfitPartKeyTag>();
                if (tag == null)
                {
                    tag = smr.gameObject.AddComponent<OutfitPartKeyTag>();
                    addedTags++;
                }

                var key = GetOrCreateOutfitKeyAsset(outfitDataRootPath, smr.name, slot, ref createdAssets, ref updatedAssets);

                tag.SetOutfitKeyForEditor(key);
                configuredKeys.Add(key);
                configuredTags++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, templatePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int CountEmptyTagsInTemplate(string templatePrefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        try
        {
            var emptyTags = FindEmptyOutfitPartTags(root);
            return emptyTags.Count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int RemoveUnsupportedEmptyTagsInTemplate(string templatePrefabPath)
    {
        int removedCount = 0;
        var root = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        try
        {
            var emptyTags = FindEmptyOutfitPartTags(root);
            for (int i = 0; i < emptyTags.Count; i++)
            {
                var tag = emptyTags[i];
                if (tag == null)
                    continue;

                if (OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(tag.gameObject.name, out _))
                    continue;

                UnityEngine.Object.DestroyImmediate(tag, true);
                removedCount++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, templatePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return removedCount;
    }

    private static void AssignOutfitKeysToEmptyTagsInTemplate(
        string templatePrefabPath,
        string outfitDataRootPath,
        System.Collections.Generic.HashSet<OutfitKeySO> configuredKeys,
        ref int createdAssets,
        ref int updatedAssets,
        ref int assignedCount,
        ref int skippedNoSlot)
    {
        var root = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        try
        {
            var emptyTags = FindEmptyOutfitPartTags(root);
            for (int i = 0; i < emptyTags.Count; i++)
            {
                var tag = emptyTags[i];
                if (tag == null)
                    continue;

                if (!OutfitAutoGenerationRules.IsAllowedOutfitIdentifier(tag.gameObject.name))
                {
                    skippedNoSlot++;
                    continue;
                }

                if (!OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(tag.gameObject.name, out var slot))
                {
                    skippedNoSlot++;
                    continue;
                }

                var key = GetOrCreateOutfitKeyAsset(outfitDataRootPath, tag.gameObject.name, slot, ref createdAssets, ref updatedAssets);

                tag.SetOutfitKeyForEditor(key);
                configuredKeys.Add(key);
                assignedCount++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, templatePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsTopPartName(string name)
    {
        return OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(name, out var slot) &&
               slot == Enums.EquippedItemSlotType.Body;
    }

    private static bool IsBottomPartName(string name)
    {
        return OutfitAutoGenerationRules.TryGetAutoOutfitSlotByPartName(name, out var slot) &&
               slot == Enums.EquippedItemSlotType.Foot;
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "OutfitKey";

        var chars = raw.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0)
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void EnsureOutfitKeysAddressable(System.Collections.Generic.IEnumerable<OutfitKeySO> keys)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[OutfitTemplateSetupTool] Addressable settings not found.");
            return;
        }

        var targetGroup = settings.FindGroup("Character Outfit Data") ?? settings.DefaultGroup;
        if (targetGroup == null)
        {
            Debug.LogError("[OutfitTemplateSetupTool] Unable to resolve target Addressables group.");
            return;
        }

        foreach (var key in keys)
        {
            if (key == null)
                continue;

            var path = AssetDatabase.GetAssetPath(key);
            if (string.IsNullOrEmpty(path))
                continue;

            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, targetGroup, false, true);
            }
            else if (entry.parentGroup != targetGroup)
            {
                settings.MoveEntry(entry, targetGroup, false, true);
            }

            if (entry == null)
                continue;

            entry.address = Path.GetFileNameWithoutExtension(path);
            entry.SetLabel("PreLoad_DataSO", true);
            entry.SetLabel("Scriptable Object", true);
        }
    }

    private static void RestoreLegacyOutfitKeyGroup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        var legacyGroup = settings.FindGroup("Character Outfit Data");
        if (legacyGroup == null)
            return;

        MoveOutfitKeyToGroupByAssetName(settings, legacyGroup, "OutfitKey_Fighter_Body");
        MoveOutfitKeyToGroupByAssetName(settings, legacyGroup, "OutfitKey_Fighter_Head_None");
        MoveOutfitKeyToGroupByAssetName(settings, legacyGroup, "OutfitKey_Fighter_Headband");
    }

    private static void MoveOutfitKeyToGroupByAssetName(AddressableAssetSettings settings, AddressableAssetGroup group, string assetName)
    {
        if (settings == null || group == null || string.IsNullOrEmpty(assetName))
            return;

        var guidArray = AssetDatabase.FindAssets("t:OutfitKeySO");
        for (int i = 0; i < guidArray.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guidArray[i]);
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, assetName, StringComparison.Ordinal))
                continue;

            var entry = settings.FindAssetEntry(guidArray[i]);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guidArray[i], group, false, true);
            }
            else if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group, false, true);
            }

            if (entry != null)
            {
                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel("PreLoad_DataSO", true);
                entry.SetLabel("Scriptable Object", true);
            }

            return;
        }
    }


    private static System.Collections.Generic.List<OutfitPartKeyTag> FindEmptyOutfitPartTags(GameObject root)
    {
        var result = new System.Collections.Generic.List<OutfitPartKeyTag>();
        if (root == null)
            return result;

        var tags = root.GetComponentsInChildren<OutfitPartKeyTag>(true);
        for (int i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            if (tag == null)
                continue;

            if (!HasAssignedOutfitKeyData(tag))
                result.Add(tag);
        }

        return result;
    }

    private static bool HasAssignedOutfitKeyData(OutfitPartKeyTag tag)
    {
        if (tag == null)
            return false;

        var so = new SerializedObject(tag);
        var assetRefProp = so.FindProperty("outfitKeyReference");
        var guidProp = assetRefProp?.FindPropertyRelative("m_AssetGUID");

        return guidProp != null && !string.IsNullOrEmpty(guidProp.stringValue);
    }

    private static OutfitKeySO GetOrCreateOutfitKeyAsset(string outfitDataRootPath, string id, Enums.EquippedItemSlotType slot, ref int createdAssets, ref int updatedAssets)
    {
        var folderPath = GetOutfitKeyFolderPath(outfitDataRootPath, slot);
        EnsureFolder(folderPath);

        var assetName = SanitizeFileName($"OutfitKey_{id}");
        var assetPath = $"{folderPath}/{assetName}.asset";

        var key = AssetDatabase.LoadAssetAtPath<OutfitKeySO>(assetPath);
        if (key == null)
        {
            key = FindOutfitKeyAssetByNameInBaseFolder(outfitDataRootPath, assetName);
            if (key != null)
            {
                var existingPath = AssetDatabase.GetAssetPath(key);
                if (!string.Equals(existingPath, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    var moveError = AssetDatabase.MoveAsset(existingPath, assetPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        Debug.LogError($"[OutfitTemplateSetupTool] Failed to move OutfitKey asset: {moveError}");
                    }

                    key = AssetDatabase.LoadAssetAtPath<OutfitKeySO>(assetPath);
                }
            }
        }

        if (key == null)
        {
            key = ScriptableObject.CreateInstance<OutfitKeySO>();
            key.id = id;
            AssetDatabase.CreateAsset(key, assetPath);
            createdAssets++;
            return key;
        }

        key.id = id;
        EditorUtility.SetDirty(key);
        updatedAssets++;
        return key;
    }

    private static string GetOutfitKeyFolderPath(string outfitDataRootPath, Enums.EquippedItemSlotType slot)
    {
        return slot switch
        {
            Enums.EquippedItemSlotType.Body => $"{outfitDataRootPath}/Top",
            Enums.EquippedItemSlotType.Foot => $"{outfitDataRootPath}/Bottom",
            Enums.EquippedItemSlotType.Head => $"{outfitDataRootPath}/HeadGear",
            _ => $"{outfitDataRootPath}/Others"
        };
    }

    private static OutfitKeySO FindOutfitKeyAssetByNameInBaseFolder(string outfitDataRootPath, string assetName)
    {
        var guidArray = AssetDatabase.FindAssets($"{assetName} t:OutfitKeySO", new[] { outfitDataRootPath });
        for (int i = 0; i < guidArray.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guidArray[i]);
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, assetName, StringComparison.Ordinal))
                continue;

            return AssetDatabase.LoadAssetAtPath<OutfitKeySO>(path);
        }

        return null;
    }

    private static System.Collections.Generic.List<string> ResolveTemplatePrefabPaths()
    {
        var paths = new System.Collections.Generic.List<string>();
        for (int i = 0; i < TemplatePrefabAddressKeys.Length; i++)
        {
            var path = ResolvePathByAddressKey(TemplatePrefabAddressKeys[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            if (!paths.Contains(path))
                paths.Add(path);
        }

        if (paths.Count == 0)
            Debug.LogError("[OutfitTemplateSetupTool] No template prefab path resolved.");

        return paths;
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        return EditorAddressablePathResolver.ResolvePathByMapIdOrAddressKey(
            addressKey,
            nameof(OutfitTemplateSetupTool));
    }
}
#endif

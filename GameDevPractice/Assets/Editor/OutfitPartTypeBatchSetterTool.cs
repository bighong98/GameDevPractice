#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Temporary data setup tool.
/// Fills OutfitKeySO.partType using OutfitPartTypeSO assets found by Addressables key.
/// </summary>
public static class OutfitPartTypeBatchSetterTool
{
    private const string OutfitPartTypeDataAddressKey = "Outfit Part Type Data";
    private const string OutfitDataAddressKey = "Outfit Data";

    [MenuItem("Tools/Outfit/Temp/Assign OutfitKey PartType From Addressable Data")]
    private static void AssignOutfitKeyPartTypes()
    {
        var partTypeFolderPath = ResolvePathByAddressKey(OutfitPartTypeDataAddressKey);
        if (string.IsNullOrEmpty(partTypeFolderPath))
        {
            Debug.LogError($"[{nameof(OutfitPartTypeBatchSetterTool)}] Failed to resolve address key: {OutfitPartTypeDataAddressKey}");
            return;
        }

        var partTypes = LoadAssetsInFolder<OutfitPartTypeSO>(partTypeFolderPath);
        if (partTypes.Count == 0)
        {
            Debug.LogWarning($"[{nameof(OutfitPartTypeBatchSetterTool)}] No OutfitPartTypeSO found in '{partTypeFolderPath}'");
            return;
        }

        var outfitDataPath = ResolvePathByAddressKey(OutfitDataAddressKey);
        var outfitKeys = LoadOutfitKeys(outfitDataPath);
        if (outfitKeys.Count == 0)
        {
            Debug.LogWarning($"[{nameof(OutfitPartTypeBatchSetterTool)}] No OutfitKeySO found.");
            return;
        }

        int updated = 0;
        int skippedAlreadyAssigned = 0;
        int unresolved = 0;
        var unresolvedPaths = new List<string>();

        for (int i = 0; i < outfitKeys.Count; i++)
        {
            var key = outfitKeys[i];
            if (key == null)
                continue;

            if (key.partType != null)
            {
                skippedAlreadyAssigned++;
                continue;
            }

            var keyPath = AssetDatabase.GetAssetPath(key);
            var resolved = ResolvePartTypeForKey(key, keyPath, partTypes);
            if (resolved == null)
            {
                unresolved++;
                unresolvedPaths.Add(keyPath);
                continue;
            }

            key.partType = resolved;
            EditorUtility.SetDirty(key);
            updated++;
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        if (unresolved > 0)
        {
            var preview = string.Join("\n", unresolvedPaths.Take(20));
            Debug.LogWarning(
                $"[{nameof(OutfitPartTypeBatchSetterTool)}] Unresolved OutfitKeySO: {unresolved}\n" +
                $"(showing up to 20)\n{preview}");
        }

        Debug.Log(
            $"[{nameof(OutfitPartTypeBatchSetterTool)}] Done. " +
            $"OutfitKeys={outfitKeys.Count}, Updated={updated}, SkippedAlreadyAssigned={skippedAlreadyAssigned}, Unresolved={unresolved}");
    }

    private static OutfitPartTypeSO ResolvePartTypeForKey(OutfitKeySO key, string keyPath, List<OutfitPartTypeSO> partTypes)
    {
        var folderName = Path.GetFileName(Path.GetDirectoryName(keyPath) ?? string.Empty);
        if (TryResolveByFolderName(folderName, partTypes, out var byFolder))
            return byFolder;

        return null;
    }

    private static bool TryResolveByFolderName(string folderName, List<OutfitPartTypeSO> partTypes, out OutfitPartTypeSO result)
    {
        result = null;
        var token = Normalize(folderName);
        if (string.IsNullOrEmpty(token))
            return false;

        var matches = new List<OutfitPartTypeSO>();
        for (int i = 0; i < partTypes.Count; i++)
        {
            var partType = partTypes[i];
            if (partType == null)
                continue;

            var nameToken = Normalize(partType.name);
            var idToken = Normalize(partType.id);

            if (IsTokenMatch(token, nameToken) || IsTokenMatch(token, idToken))
                matches.Add(partType);
        }

        if (matches.Count != 1)
            return false;

        result = matches[0];
        return true;
    }

    private static bool IsTokenMatch(string folderToken, string sourceToken)
    {
        if (string.IsNullOrEmpty(sourceToken))
            return false;

        if (folderToken == sourceToken)
            return true;

        if (sourceToken.StartsWith("outfit") && sourceToken.EndsWith(folderToken, StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw.ToCharArray();
        var buffer = new char[chars.Length];
        int count = 0;

        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (char.IsLetterOrDigit(c))
            {
                buffer[count] = char.ToLowerInvariant(c);
                count++;
            }
        }

        return new string(buffer, 0, count);
    }

    private static List<T> LoadAssetsInFolder<T>(string folderPath) where T : UnityEngine.Object
    {
        var result = new List<T>();
        if (string.IsNullOrEmpty(folderPath))
            return result;

        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                result.Add(asset);
        }

        return result;
    }

    private static List<OutfitKeySO> LoadOutfitKeys(string outfitDataPath)
    {
        var result = new List<OutfitKeySO>();
        string[] guids;

        if (!string.IsNullOrEmpty(outfitDataPath) && AssetDatabase.IsValidFolder(outfitDataPath))
            guids = AssetDatabase.FindAssets("t:OutfitKeySO", new[] { outfitDataPath });
        else
            guids = AssetDatabase.FindAssets("t:OutfitKeySO");

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var key = AssetDatabase.LoadAssetAtPath<OutfitKeySO>(path);
            if (key != null)
                result.Add(key);
        }

        return result;
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return null;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return null;

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

        return null;
    }
}
#endif

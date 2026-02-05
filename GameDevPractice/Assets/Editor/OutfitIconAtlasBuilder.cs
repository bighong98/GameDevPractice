#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class OutfitIconAtlasBuilder
{
    private const string OutputIconsAddressKey = "Outfit Output Icons";
    private const string AtlasAssetName = "OutfitOutputIcons";

    [MenuItem("Tools/Outfit/Build Outfit Icon Sprite Atlas")]
    private static void BuildOutfitIconSpriteAtlas()
    {
        var iconFolderPath = ResolvePathByAddressKey(OutputIconsAddressKey);
        if (string.IsNullOrEmpty(iconFolderPath))
            return;

        if (!AssetDatabase.IsValidFolder(iconFolderPath))
        {
            Debug.LogError($"[OutfitIconAtlasBuilder] Icon folder not found: {iconFolderPath}");
            return;
        }

        var atlasPath = $"{iconFolderPath}/{AtlasAssetName}.spriteatlas";
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

        bool created = false;
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
            created = true;
        }

        var reimportedCount = ConfigureIconTextureImporters(iconFolderPath);

        ApplyAtlasSettings(atlas);
        ReplacePackablesWithIconFolder(atlas, iconFolderPath);

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.Refresh();

        var spriteCount = AssetDatabase.FindAssets("t:Texture2D", new[] { iconFolderPath }).Length;
        Debug.Log($"[OutfitIconAtlasBuilder] {(created ? "Created" : "Updated")} atlas: {atlasPath}, PackedSprites={spriteCount}, ReimportedUncompressed={reimportedCount}, BuildTarget={EditorUserBuildSettings.activeBuildTarget}");
    }

    private static void ApplyAtlasSettings(SpriteAtlas atlas)
    {
        var packing = new SpriteAtlasPackingSettings
        {
            blockOffset = 1,
            padding = 4,
            enableRotation = true,
            enableTightPacking = false,
            enableAlphaDilation = false
        };

        var texture = new SpriteAtlasTextureSettings
        {
            readable = false,
            generateMipMaps = false,
            sRGB = true,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        SpriteAtlasExtensions.SetIsVariant(atlas, false);
        SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
        SpriteAtlasExtensions.SetPackingSettings(atlas, packing);
        SpriteAtlasExtensions.SetTextureSettings(atlas, texture);
    }

    private static void ReplacePackablesWithIconFolder(SpriteAtlas atlas, string iconFolderPath)
    {
        var current = SpriteAtlasExtensions.GetPackables(atlas);
        if (current != null && current.Length > 0)
            SpriteAtlasExtensions.Remove(atlas, current);

        var folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(iconFolderPath);
        if (folderAsset == null)
        {
            Debug.LogError($"[OutfitIconAtlasBuilder] Failed to load icon folder asset: {iconFolderPath}");
            return;
        }

        SpriteAtlasExtensions.Add(atlas, new[] { folderAsset });
    }

    private static int ConfigureIconTextureImporters(string iconFolderPath)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { iconFolderPath });
        int reimportedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            var texturePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!(AssetImporter.GetAtPath(texturePath) is TextureImporter importer))
                continue;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.crunchedCompression)
            {
                importer.crunchedCompression = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                reimportedCount++;
            }
        }

        return reimportedCount;
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return null;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError($"[OutfitIconAtlasBuilder] Addressable settings not found. key={addressKey}");
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

        Debug.LogError($"[OutfitIconAtlasBuilder] Addressable key not found: {addressKey}");
        return null;
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class InventoryItemIconAtlasBuilder
{
    private const string MenuPath = "Tools/Item/Atlas/Build Inventory Icon Sprite Atlas";

    private const string ItemDataGroupName = "Item Data";
    private const string UiGroupName = "UIs";
    private const string PreLoadAtlasLabel = "PreLoad_Atlas";

    private const string AtlasAssetPath = "Assets/Renderings/Sprite Atlas/InventoryItemIcons.spriteatlas";
    private const string AtlasAddress = "InventoryItemIcons.spriteatlas";

    private sealed class ItemRecord
    {
        public ItemTypeSO Item;
        public string AssetPath;
    }

    [MenuItem(MenuPath)]
    private static void BuildInventoryItemIconSpriteAtlas()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError($"[{nameof(InventoryItemIconAtlasBuilder)}] Addressable settings not found.");
            return;
        }

        var itemDataGroup = settings.FindGroup(ItemDataGroupName);
        if (itemDataGroup == null)
        {
            Debug.LogError($"[{nameof(InventoryItemIconAtlasBuilder)}] Addressables group not found: {ItemDataGroupName}");
            return;
        }

        var itemRecords = CollectItemTypeRecords(itemDataGroup, out var skippedEntries);
        var iconSprites = CollectIconSprites(itemRecords, out var missingIconCount, out var missingLogs);

        EnsureFolder(Path.GetDirectoryName(AtlasAssetPath)?.Replace("\\", "/"));

        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasAssetPath);
        bool created = false;
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, AtlasAssetPath);
            created = true;
        }

        ApplyAtlasSettings(atlas);
        ReplacePackables(atlas, iconSprites);

        EditorUtility.SetDirty(atlas);
        EnsureAddressableEntry(settings);

        AssetDatabase.SaveAssets();

        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.Refresh();

        for (int i = 0; i < missingLogs.Count; i++)
        {
            Debug.LogWarning(missingLogs[i]);
        }

        Debug.Log(
            $"[{nameof(InventoryItemIconAtlasBuilder)}] {(created ? "Created" : "Updated")} atlas: {AtlasAssetPath}, " +
            $"ItemRecords={itemRecords.Count}, SkippedEntries={skippedEntries}, UniqueIcons={iconSprites.Count}, MissingIcons={missingIconCount}, " +
            $"Address={AtlasAddress}, Label={PreLoadAtlasLabel}, BuildTarget={EditorUserBuildSettings.activeBuildTarget}");
    }

    private static List<ItemRecord> CollectItemTypeRecords(AddressableAssetGroup group, out int skippedEntries)
    {
        skippedEntries = 0;
        var records = new List<ItemRecord>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in group.entries)
        {
            if (entry == null)
            {
                skippedEntries++;
                continue;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                skippedEntries++;
                continue;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                var guids = AssetDatabase.FindAssets("t:ItemTypeSO", new[] { assetPath });
                for (int i = 0; i < guids.Length; i++)
                {
                    var nestedPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!TryAddItemRecord(nestedPath, records, seenPaths))
                    {
                        skippedEntries++;
                    }
                }

                continue;
            }

            if (!TryAddItemRecord(assetPath, records, seenPaths))
            {
                skippedEntries++;
            }
        }

        records.Sort((left, right) => string.CompareOrdinal(left.AssetPath, right.AssetPath));
        return records;
    }

    private static bool TryAddItemRecord(string assetPath, List<ItemRecord> records, HashSet<string> seenPaths)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var normalizedPath = assetPath.Replace("\\", "/");
        if (!seenPaths.Add(normalizedPath))
        {
            return true;
        }

        var item = AssetDatabase.LoadAssetAtPath<ItemTypeSO>(normalizedPath);
        if (item == null)
        {
            return false;
        }

        records.Add(new ItemRecord
        {
            Item = item,
            AssetPath = normalizedPath
        });

        return true;
    }

    private static List<UnityEngine.Object> CollectIconSprites(List<ItemRecord> itemRecords, out int missingIconCount, out List<string> missingLogs)
    {
        missingIconCount = 0;
        missingLogs = new List<string>();

        var sprites = new List<UnityEngine.Object>(itemRecords.Count);
        var seenSpriteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < itemRecords.Count; i++)
        {
            var record = itemRecords[i];

            if (!TryResolveItemIconSprite(record.Item, out var sprite, out var reason))
            {
                missingIconCount++;
                missingLogs.Add($"[{nameof(InventoryItemIconAtlasBuilder)}] Missing icon. ItemPath={record.AssetPath}, Reason={reason}");
                continue;
            }

            var key = BuildSpriteKey(sprite);
            if (!seenSpriteKeys.Add(key))
            {
                continue;
            }

            sprites.Add(sprite);
        }

        return sprites;
    }

    private static bool TryResolveItemIconSprite(ItemTypeSO item, out Sprite sprite, out string reason)
    {
        sprite = null;
        reason = string.Empty;

        if (item == null)
        {
            reason = "ItemTypeSO is null";
            return false;
        }

        var serializedObject = new SerializedObject(item);
        var spriteReferenceProp = serializedObject.FindProperty("spriteReference");
        if (spriteReferenceProp == null)
        {
            reason = "Serialized field not found: spriteReference";
            return false;
        }

        var guidProp = spriteReferenceProp.FindPropertyRelative("m_AssetGUID");
        var subObjectNameProp = spriteReferenceProp.FindPropertyRelative("m_SubObjectName");

        var spriteGuid = guidProp?.stringValue;
        var subObjectName = subObjectNameProp?.stringValue;

        if (string.IsNullOrWhiteSpace(spriteGuid))
        {
            reason = "spriteReference GUID is empty";
            return false;
        }

        var spriteAssetPath = AssetDatabase.GUIDToAssetPath(spriteGuid);
        if (string.IsNullOrWhiteSpace(spriteAssetPath))
        {
            reason = $"spriteReference GUID not resolved: {spriteGuid}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(subObjectName))
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(spriteAssetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite namedSprite &&
                    string.Equals(namedSprite.name, subObjectName, StringComparison.Ordinal))
                {
                    sprite = namedSprite;
                    return true;
                }
            }
        }

        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
        if (sprite != null)
        {
            return true;
        }

        var fallbackAssets = AssetDatabase.LoadAllAssetsAtPath(spriteAssetPath);
        for (int i = 0; i < fallbackAssets.Length; i++)
        {
            if (fallbackAssets[i] is Sprite fallbackSprite)
            {
                sprite = fallbackSprite;
                return true;
            }
        }

        reason = $"No sprite sub asset found. SpriteAssetPath={spriteAssetPath}, SubObjectName={subObjectName}";
        return false;
    }

    private static string BuildSpriteKey(Sprite sprite)
    {
        if (sprite == null)
        {
            return string.Empty;
        }

        var spritePath = AssetDatabase.GetAssetPath(sprite);
        return $"{spritePath}::{sprite.name}";
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

    private static void ReplacePackables(SpriteAtlas atlas, List<UnityEngine.Object> sprites)
    {
        var currentPackables = SpriteAtlasExtensions.GetPackables(atlas);
        if (currentPackables != null && currentPackables.Length > 0)
        {
            SpriteAtlasExtensions.Remove(atlas, currentPackables);
        }

        if (sprites == null || sprites.Count == 0)
        {
            return;
        }

        SpriteAtlasExtensions.Add(atlas, sprites.ToArray());
    }

    private static void EnsureAddressableEntry(AddressableAssetSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        var atlasGuid = AssetDatabase.AssetPathToGUID(AtlasAssetPath);
        if (string.IsNullOrWhiteSpace(atlasGuid))
        {
            Debug.LogError($"[{nameof(InventoryItemIconAtlasBuilder)}] Failed to resolve atlas guid: {AtlasAssetPath}");
            return;
        }

        settings.AddLabel(PreLoadAtlasLabel, false);

        var uiGroup = settings.FindGroup(UiGroupName);
        if (uiGroup == null)
        {
            Debug.LogError($"[{nameof(InventoryItemIconAtlasBuilder)}] Addressables group not found: {UiGroupName}");
            return;
        }

        var entry = settings.FindAssetEntry(atlasGuid);
        if (entry == null)
        {
            entry = settings.CreateOrMoveEntry(atlasGuid, uiGroup, false, true);
        }
        else if (entry.parentGroup != uiGroup)
        {
            settings.MoveEntry(entry, uiGroup, false, true);
        }

        if (entry == null)
        {
            Debug.LogError($"[{nameof(InventoryItemIconAtlasBuilder)}] Failed to create/move addressable entry for atlas.");
            return;
        }

        entry.address = AtlasAddress;
        entry.SetLabel(PreLoadAtlasLabel, true);

        EditorUtility.SetDirty(settings);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var normalized = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parent = Path.GetDirectoryName(normalized)?.Replace("\\", "/");
        var folderName = Path.GetFileName(normalized);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
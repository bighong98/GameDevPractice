#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "EditorAddressablePathMap", menuName = "Tools/Addressables/Editor Addressable Path Map")]
public sealed class EditorAddressablePathMapSO : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string mapId;
        public string addressableKey;
    }

    public List<Entry> entries = new();

    [MenuItem("Tools/Addressables/Create Default Editor Addressable Path Map")]
    private static void CreateDefaultAsset()
    {
        const string defaultAssetPath = "Assets/Editor/Scriptable Object/EditorAddressablePathMap.asset";
        var existing = AssetDatabase.LoadAssetAtPath<EditorAddressablePathMapSO>(defaultAssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"[EditorAddressablePathMapSO] Already exists: {defaultAssetPath}");
            return;
        }

        EnsureFolder("Assets/Editor/Scriptable Object");

        var asset = CreateInstance<EditorAddressablePathMapSO>();
        asset.entries = BuildDefaultEntries();
        AssetDatabase.CreateAsset(asset, defaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"[EditorAddressablePathMapSO] Created: {defaultAssetPath}");
    }

    private static List<Entry> BuildDefaultEntries()
    {
        return new List<Entry>
        {
            new() { mapId = "outfit.source_presets.folder", addressableKey = "Outfit Source Presets" },
            new() { mapId = "outfit.source_presets_headwear.folder", addressableKey = "Outfit Source Presets_wHeadwear" },
            new() { mapId = "outfit.output_presets.folder", addressableKey = "Outfit Output Presets" },
            new() { mapId = "outfit.output_presets_headwear.folder", addressableKey = "Outfit Output Presets_wHeadwear" },
            new() { mapId = "outfit.template.female", addressableKey = "BasicHero_F Variant Template" },
            new() { mapId = "outfit.template.male", addressableKey = "BasicHero_M Variant Template" },
            new() { mapId = "outfit.data.folder", addressableKey = "Outfit Data" },
            new() { mapId = "outfit.icons_output.folder", addressableKey = "Outfit Output Icons" },
            new() { mapId = "armor.output_data.folder", addressableKey = "Armor Output Data" },
            new() { mapId = "outfit.part_type_data.folder", addressableKey = "Outfit Part Type Data" },
            new() { mapId = "skill.data.folder", addressableKey = "Skill Data Folder" },
            new() { mapId = "item.weapon_data.folder", addressableKey = "Item Data Folder/Weapon" }
        };
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = System.IO.Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif

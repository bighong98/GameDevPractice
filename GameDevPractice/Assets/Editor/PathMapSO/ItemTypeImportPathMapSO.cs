#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemTypeImportPathMap", menuName = "Tools/Item/Data Sync/ItemType Import Path Map")]
public sealed class ItemTypeImportPathMapSO : ScriptableObject
{
    public List<Entry> entries = new();

    [Serializable]
    public sealed class Entry
    {
        [Tooltip("AssemblyQualifiedName or full type name. Example: TH.Resource.ArmorTypeSO")]
        public string dataTypeName;

        [Tooltip("Enum int value of Enums.ItemType")]
        public int itemTypeValue;

        [Tooltip("Addressables key that resolves to target folder (or asset inside target folder)")]
        public string targetFolderAddressableKey;
    }

    [MenuItem("Tools/Item/Data Sync/Create Default Import Path Map Asset")]
    private static void CreateDefaultAsset()
    {
        const string defaultAssetPath = "Assets/Editor/DataSync/ItemTypeImportPathMap.asset";

        var existing = AssetDatabase.LoadAssetAtPath<ItemTypeImportPathMapSO>(defaultAssetPath);
        if (existing != null)
        {
            EditorGUIUtility.PingObject(existing);
            Selection.activeObject = existing;
            Debug.Log($"[ItemTypeImportPathMapSO] Already exists: {defaultAssetPath}");
            return;
        }

        EnsureFolder("Assets/Editor/DataSync");

        var asset = CreateInstance<ItemTypeImportPathMapSO>();
        AssetDatabase.CreateAsset(asset, defaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
        Debug.Log($"[ItemTypeImportPathMapSO] Created: {defaultAssetPath}");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var normalized = folderPath.Replace("\\", "/");
        var parent = System.IO.Path.GetDirectoryName(normalized)?.Replace("\\", "/");
        var name = System.IO.Path.GetFileName(normalized);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
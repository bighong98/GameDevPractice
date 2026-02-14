#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTypeImportPathMap", menuName = "Tools/Skill/Data Sync/SkillType Import Path Map")]
public sealed class SkillTypeImportPathMapSO : ScriptableObject
{
    [Tooltip("Addressable key that points to the root folder for SkillTypeSO sync")]
    public string skillDataFolderAddressableKey = "Skill Data Folder";

    public List<Entry> entries = new();

    [Serializable]
    public sealed class Entry
    {
        [Tooltip("AssemblyQualifiedName or full type name. Example: TH.Resource.SkillTypeSO")]
        public string dataTypeName;

        [Tooltip("Enum int value of DamageType")]
        public int damageTypeValue;

        [Tooltip("Addressables key that resolves to target folder (or asset inside target folder)")]
        public string targetFolderAddressableKey;
    }

    [MenuItem("Tools/Skill/Data Sync/Create Default Import Path Map Asset")]
    private static void CreateDefaultAsset()
    {
        const string defaultAssetPath = "Assets/Editor/Scriptable Object/SkillTypeImportPathMap.asset";

        var existing = AssetDatabase.LoadAssetAtPath<SkillTypeImportPathMapSO>(defaultAssetPath);
        if (existing != null)
        {
            EditorGUIUtility.PingObject(existing);
            Selection.activeObject = existing;
            Debug.Log($"[SkillTypeImportPathMapSO] Already exists: {defaultAssetPath}");
            return;
        }

        EnsureFolder("Assets/Editor/Scriptable Object");

        var asset = CreateInstance<SkillTypeImportPathMapSO>();
        AssetDatabase.CreateAsset(asset, defaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
        Debug.Log($"[SkillTypeImportPathMapSO] Created: {defaultAssetPath}");
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

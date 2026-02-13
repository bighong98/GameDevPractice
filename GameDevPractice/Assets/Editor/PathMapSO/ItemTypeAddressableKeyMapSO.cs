#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemTypeAddressableKeyMap", menuName = "Tools/Item/Data Sync/ItemType Addressable Key Map")]
public sealed class ItemTypeAddressableKeyMapSO : ScriptableObject
{
    [Tooltip("Addressable key that points to the root folder for ItemTypeSO sync")]
    public string itemDataFolderAddressableKey = "Item Data Folder";

    [MenuItem("Tools/Item/Data Sync/Create Default Addressable Key Map Asset")]
    private static void CreateDefaultAsset()
    {
        const string defaultAssetPath = "Assets/Editor/Scriptable Object/ItemTypeAddressableKeyMap.asset";

        var existing = AssetDatabase.LoadAssetAtPath<ItemTypeAddressableKeyMapSO>(defaultAssetPath);
        if (existing != null)
        {
            EditorGUIUtility.PingObject(existing);
            Selection.activeObject = existing;
            Debug.Log($"[ItemTypeAddressableKeyMapSO] Already exists: {defaultAssetPath}");
            return;
        }

        EnsureFolder("Assets/Editor/Scriptable Object");

        var asset = CreateInstance<ItemTypeAddressableKeyMapSO>();
        AssetDatabase.CreateAsset(asset, defaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
        Debug.Log($"[ItemTypeAddressableKeyMapSO] Created: {defaultAssetPath}");
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
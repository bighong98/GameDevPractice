using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public static class CreateQuickSlotPanelPrefab
{
    private const string CreatedKey = "CreateQuickSlotPanelPrefab_AlreadyCreated";
    
    static CreateQuickSlotPanelPrefab()
    {
        if (EditorPrefs.GetBool(CreatedKey, false))
            return;
        
        EditorApplication.delayCall += CreatePrefab;
    }
    
    private static void CreatePrefab()
    {
        string prefabPath = "Assets/Game/UI/SubItem/SceneUI/QuickSlotPanel.prefab";
        
        // Find QuickSlotPanel in the scene
        GameObject quickSlotPanel = GameObject.Find("QuickSlotPanel");
        
        if (quickSlotPanel == null)
        {
            Debug.LogError("QuickSlotPanel GameObject not found in scene!");
            EditorPrefs.SetBool(CreatedKey, true);
            return;
        }
        
        // Create directory if it doesn't exist
        string directory = System.IO.Path.GetDirectoryName(prefabPath);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            string[] folders = directory.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }
        
        // Save as prefab
        bool success = false;
        PrefabUtility.SaveAsPrefabAsset(quickSlotPanel, prefabPath, out success);
        
        if (success)
        {
            Debug.Log($"Successfully created prefab: {prefabPath}");
        }
        else
        {
            Debug.LogError($"Failed to create prefab: {prefabPath}");
        }
        
        EditorPrefs.SetBool(CreatedKey, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    [MenuItem("Tools/Reset CreateQuickSlotPanelPrefab Flag")]
    private static void ResetFlag()
    {
        EditorPrefs.DeleteKey(CreatedKey);
        Debug.Log("Flag reset. Prefab will be created on next domain reload.");
    }
}
#endif

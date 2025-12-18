using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "SceneCatalogSO", menuName = "Scriptable Objects/SceneCatalogSO")]
public class SceneCatalogSO : ScriptableObject
{
    public List<SceneEntry> entries = new();
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        bool dirty = false;
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e == null) continue;
                var sceneRef = e.sceneRef;
                e.sceneId = sceneRef?.AssetGUID;
                e.key = Util.GetAddressKeyInEditor(sceneRef);
                dirty = true;
            }
        }
        
        if (dirty)
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
    private string CurrSceneName => SceneManager.GetActiveScene().name;
    public bool TryGetCurrentSceneEntry(out SceneEntry sceneEntry)
    {
        return TryGetSceneEntry(CurrSceneName, out sceneEntry);
    }

    public bool TryGetSceneEntry(Scene scene, out SceneEntry sceneEntry)
    {
        return TryGetSceneEntry(scene.name, out sceneEntry);
    }
    
    private bool TryGetSceneEntry(string sceneName, out SceneEntry sceneEntry)
    {
        sceneEntry = null;
        if (sceneName == null || string.IsNullOrEmpty(sceneName))
        {
            return false;
        }
        
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.key != sceneName) continue;
            
            sceneEntry = e;
            return true;
        }
        
        return false;
    }

    public SceneEntry GetCurrentSceneEntry()
    {
        return FindBySceneName(CurrSceneName);
    }

    public SceneEntry FindByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid) || entries == null || entries.Count == 0) return null;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (entry.sceneId != guid) continue;
            return entry;
        }

        return null;
    }
    
    public SceneEntry FindBySceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || entries == null || entries.Count == 0) return null;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (entry.key != sceneName) continue;
            return entry;
        }

        return null;
    }
}

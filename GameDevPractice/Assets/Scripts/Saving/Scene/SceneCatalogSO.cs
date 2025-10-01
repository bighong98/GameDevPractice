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

    public SceneEntry GetCurrentSceneEntry()
    {
        var curr = SceneManager.GetActiveScene().name;

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.key == curr)
            {
                return e;
            }
        }

        return null;
    }
}

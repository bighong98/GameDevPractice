using System;
using TH.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace TH.SaveLoad
{
    [Serializable]
    public sealed class SceneEntry
    {
        public string key;
        public string sceneId;
        public AssetReferenceScene sceneRef;
        public bool SaveTargetScene = true;

        public SceneEntry() {}
        public SceneEntry(AssetReferenceScene sceneRef)
        {
            this.sceneRef = sceneRef;
            sceneId = sceneRef.AssetGUID;

#if UNITY_EDITOR
            key = Util.GetAddressKeyInEditor(sceneRef);
#endif
        } 
    }
}


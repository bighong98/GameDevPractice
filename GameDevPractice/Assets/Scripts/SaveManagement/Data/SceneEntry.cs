using System;
using TH.SceneManagement;
using UnityEditor;
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


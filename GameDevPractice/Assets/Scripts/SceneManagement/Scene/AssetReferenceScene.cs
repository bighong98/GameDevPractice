using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.SceneManagement
{
    [Serializable]
    public class AssetReferenceScene : AssetReference
    {
        [SerializeField] private string sceneName = string.Empty;
        public string SceneName => sceneName;
        
#if UNITY_EDITOR
        public AssetReferenceScene() {} // parameterless constructor for serialization 
        public AssetReferenceScene(SceneAsset scene)
        : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene)))
        {
            sceneName = scene.name;
            Util.Log($"[{nameof(AssetReferenceScene)}] sceneName: '{sceneName}'");
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
        }

        public override bool ValidateAsset(UnityEngine.Object obj)
        {
            return obj is SceneAsset;
        }

        public override bool SetEditorAsset(UnityEngine.Object obj)
        {
            if (!base.SetEditorAsset(obj))
            {
                return false;
            }

            if (obj is SceneAsset scene)
            {
                sceneName = scene.name;
                return true;
            }
            else
            {
                sceneName = string.Empty;
                return false;
            }
        }

#endif
    }
}



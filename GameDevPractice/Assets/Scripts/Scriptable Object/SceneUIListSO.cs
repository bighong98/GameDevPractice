using System;
using System.Collections.Generic;
using TH.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SceneUIListSO", menuName = "Scriptable Objects/TypeList/SceneUIListSO")]
    public class SceneUIListSO : ScriptableObject
    {
        [SerializeField] public List<SceneUIPair> list;
        public IReadOnlyCollection<SceneUIPair> SceneUIs => list;
        private readonly Dictionary<string, AssetReferenceSceneUI> SceneUIDict = new();
        
        public AssetReferenceSceneUI GetSceneUIByScene(AssetReferenceScene scene)
        {
            if (SceneUIDict.Count == 0) ForceInitDict();
            return SceneUIDict.GetValueOrDefault(scene.AssetGUID);
        }

        public void ForceInitDict()
        {
            foreach (var (scene, sceneUI) in list)
            {
                SceneUIDict[scene.AssetGUID] = sceneUI;
            }
        }
    }

    [Serializable]
    public struct SceneUIPair
    {
        public AssetReferenceScene scene;
        public AssetReferenceSceneUI sceneUI;
        
        public void Deconstruct(out AssetReferenceScene sceneRef, out AssetReferenceSceneUI sceneUIRef)
        {
            sceneRef = scene;
            sceneUIRef = sceneUI;
        }
    }
}


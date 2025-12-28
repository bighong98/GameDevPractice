using System;
using System.Collections.Generic;
using TH.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SceneUIListSO", menuName = "Scriptable Objects/UI/SceneUIListSO")]
    public class SceneUIListSO : ScriptableObject
    {
        [SerializeField] private List<SceneUIPair> list;
        private IReadOnlyCollection<SceneUIPair> capturedList;
        public IReadOnlyCollection<SceneUIPair> SceneUIs { get {
            if (capturedList == null || capturedList.Count == 0)
                capturedList = list.AsReadOnly();
            return capturedList; } 
        }
        
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


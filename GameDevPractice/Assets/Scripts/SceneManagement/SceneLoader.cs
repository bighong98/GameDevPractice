using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        private const string LoadingSceneKey = "LoaidngScene";
        
        public SceneLoader()
        {
            Init();
        }

        private void Init()
        {
            //todo: 로딩 씬으로 이동
        }
        
        public UniTask LoadSceneAsync(AssetReferenceScene sceneRef, CancellationToken token = default)
        {
            throw new System.NotImplementedException();
        }

        public UniTask LoadSceneAsync(string key, CancellationToken token = default)
        {
            throw new System.NotImplementedException();
        }
    }
}



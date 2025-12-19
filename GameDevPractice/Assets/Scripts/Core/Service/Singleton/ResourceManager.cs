using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.Scripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TH.Core.Service
{
    [Preserve]
    public class ResourceManager : Singleton<ResourceManager>, ISingleton
    {
        private readonly IResourceLoader resourceLoader;
        private readonly ISceneLoader sceneLoader;

        private ResourceManager()
        {
            resourceLoader = ServiceLocator.Get<IResourceLoader>(); 
            sceneLoader = ServiceLocator.Get<ISceneLoader>();
            
            sceneLoader.OnBeforeSceneChanged += BeforeSceneLoad;
            sceneLoader.OnAfterSceneChanged += AfterSceneLoad;
        }
        
        #region PreLoad

        public void WaitForPreLoadOnlyOnce(Action callback)
        {
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, callback);
        }

        #endregion

        #region Load

        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (어드레서블 키 사용)
        public T Load<T>(string key) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(key, out var result))
            {
                return result;
            }

            return null;
        }
        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (어드레서블 키 사용)
        public bool TryLoad<T>(string key, out T resource) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(key, out var result))
            {
                resource = result;
                return true;
            }

            resource = null;
            return false;
        }
        
        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (AssetReference 사용)
        public bool TryLoad<T>(AssetReference assetRef, out T resource) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(assetRef, out var result))
            {
                resource = result;
                return true;
            }

            resource = null;
            return false;
        }

        // 필요한 리소스가 프리팹인 경우에 사용
        // 리소스 로드 후 즉시 Instantiate() 실행하여 결과 게임오브젝트 반환
        // 오브젝트 풀링 적용x
        public GameObject Instantiate(string key, Transform parent = null)
        {
            if (TryLoad<UnityEngine.Object>(key, out var result) && result is GameObject origin)
            {
                GameObject clone = Object.Instantiate(origin, parent);
                clone.name = origin.name;

                return clone;
            }
            
            Logg.LogError($"{GetType().Name}.Instantiate: Failed to load prefab: {key}");
            return null;
        }
        // AssetReference기반 리소스 조회 (비동기 지원)
        // 캐시 미스(=사전 로딩이 되지 않았을 경우) 시 직접 비동기 로드 실행
        public async UniTask<T> ExtractAssetRefAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(assetRef, out var result))
            {
                return result;
            }

            return await resourceLoader.LoadAsync<T>(assetRef, token);
        }

        public async UniTask<T> ExtractAssetRefAsync<T>(AssetReferenceT<T> assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            return await ExtractAssetRefAsync<T>((AssetReference)assetRef, token);
        }

        public async UniTask<T> ExtractAssetRefAsync<T>(AssetReferenceGeneric<T> assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            return await ExtractAssetRefAsync<T>((AssetReference)assetRef, token);
        }

        #endregion
        
        #region ISingleton 
        public UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            return UniTask.CompletedTask;
        }

        public UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            return UniTask.CompletedTask;
        }
        #endregion
    }
}


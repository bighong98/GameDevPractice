using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;
using TH.Core;

namespace TH.Resource
{
    public sealed class ResourceManager : Singleton<ResourceManager>
    {
        private IResourceLoader resourceLoader;
        
        public event Action NotifyPreLoad;
        private readonly Queue<Action> reservedPreLoadTasks = new();
        
        private bool preLoadState = false;
        public bool PreLoadState => preLoadState;
        
        private const string PreLoadLabel = "PreLoad";
        
        #region Initialization

        protected override void InitOnce()
        {
            resourceLoader = ServiceLocator.Require<IResourceLoader>();
        }

        protected override void InitOnceAfterPreLoad() { }

        protected override void Init()
        {
            if (resourceLoader.IsLoadedAll(PreLoadLabel))
            {
                OnPreLoadDone();
            }
            else
            {
                resourceLoader.NotifyResourceLoad -= OnPreLoadDone; // 중복 델리게이트 누적 방지
                resourceLoader.NotifyResourceLoad += OnPreLoadDone;
            }
        }
        protected override void InitAfterPreLoad() { }

        #endregion

        protected override UniTask Clear()
        {
            if (resourceLoader != null)
                resourceLoader.NotifyResourceLoad -= OnPreLoadDone;
            return base.Clear();
        }

        #region PreLoad

        public void WaitForPreLoad(Action callback)
        {
            if (preLoadState) callback?.Invoke();
            else NotifyPreLoad += callback;
        }

        public void WaitForPreLoadOnlyOnce(Action callback)
        {
            if (preLoadState) callback?.Invoke();
            else reservedPreLoadTasks.Enqueue(callback);
        }

        private void RunReserved()
        {
            if (reservedPreLoadTasks.Count == 0) return;
            while (reservedPreLoadTasks.TryDequeue(out var task))
            {
                task?.Invoke();
            }
        }

        private void OnPreLoadDone(string label)
        {
            if (string.Equals(label, PreLoadLabel))
            {
                OnPreLoadDone();
            }
        }

        private void OnPreLoadDone()
        {
            NotifyPreLoad?.Invoke();
            RunReserved();
        }

        #endregion

        #region Load

        public T Load<T>(string key) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(key, out var result))
            {
                return result;
            }

            return null;
        }

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

        public GameObject Instantiate(string key, Transform parent = null)
        {
            if (TryLoad<UnityEngine.Object>(key, out var result) && result is GameObject origin)
            {
                GameObject clone = Object.Instantiate(origin, parent);
                clone.name = origin.name;

                return clone;
            }
            
            Util.LogError($"{nameof(ResourceManager)}.Instantiate: Failed to load prefab: {key}");
            return null;
        }

        public async UniTask<T> ExtractAssetRefAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(assetRef, out var result))
            {
                return result;
            }

            return await resourceLoader.LoadAsync<T>(assetRef, token);
        }
        
        #endregion
        
        public void Destroy(GameObject go)
        {
            if (go == null) return;
        
            Object.Destroy(go);
        }
    }
}


using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace TH.Resource
{
    public sealed class ResourceManager : Singleton<ResourceManager>
    {
        private IResourceLoader resourceLoader;
        
        public event Action<bool> NotifyPreLoad;
        private readonly Queue<Action<bool>> reservedPreLoadTasks = new();
        
        private bool preLoadState = false;
        public bool PreLoadState => preLoadState;
        
        private const string PreLoadLabel = "PreLoad";

        #region Initialization

        protected override void InitOnce()
        {
            resourceLoader = ServiceLocator.Require<IResourceLoader>();
            NotifyPreLoad += RunReserved;
            if (resourceLoader.IsLoadedAll(PreLoadLabel))
            {
                NotifyPreLoad?.Invoke(true);
            }
            else
            {
                resourceLoader.NotifyResourceLoad += (label) =>
                {
                    if (string.Equals(label, PreLoadLabel))
                    {
                        NotifyPreLoad?.Invoke(true);
                    }
                };
            }
        }

        protected override void InitOnceAfterPreLoad(bool isLoadCompleted) { }
        protected override void Init() { }
        protected override void InitAfterPreLoad(bool isLoadCompleted) { }

        #endregion
        
        protected override UniTask Clear() { return base.Clear(); }

        #region PreLoad

        public void WaitForPreLoad(Action<bool> callback)
        {
            if (preLoadState) callback?.Invoke(true);
            else NotifyPreLoad += callback;
        }

        public void WaitForPreLoadOnlyOnce(Action<bool> callback)
        {
            if (preLoadState) callback?.Invoke(true);
            else reservedPreLoadTasks.Enqueue(callback);
        }

        private void RunReserved(bool dum)
        {
            if (reservedPreLoadTasks.Count == 0) return;
            while (reservedPreLoadTasks.TryDequeue(out var task))
            {
                task?.Invoke(true);
            }
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
        
        public void Destroy(GameObject go)
        {
            if (go == null) return;
        
            Object.Destroy(go);
        }

        #endregion
    }
}


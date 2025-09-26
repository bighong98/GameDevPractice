using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace TH.Resource
{
    public interface IResourceLoader
    {
        event Action<string> NotifyResourceLoad;
        bool TryLoad<T>(string key, out T resource) where T : Object;
        bool TryLoad<T>(AssetReference assetRef, out T resource) where T : Object;
        bool IsLoadedAll(string label);
    }
}


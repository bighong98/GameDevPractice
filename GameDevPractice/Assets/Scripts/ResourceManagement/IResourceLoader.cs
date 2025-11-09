using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace TH.Resource
{
    public interface IResourceLoader
    {
        event Action<string> NotifyResourceLoad;
        UniTask<T> LoadAsync<T>(string key) where T : Object;
        UniTask<T> LoadAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : Object;
        bool TryLoad<T>(string key, out T resource) where T : Object;
        bool TryLoad<T>(AssetReference assetRef, out T resource) where T : Object;
        bool IsLoadedAll(string label);
        bool IsPreLoadDone(); // 임시 매서드

        string PreLoadLabel { get; }
    }
}


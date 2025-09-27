

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TH.SceneManagement
{
    public interface ISceneLoader
    {
        UniTask LoadSceneAsync(AssetReferenceScene sceneRef, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default);
        UniTask LoadSceneAsync(string key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default);
    }
}


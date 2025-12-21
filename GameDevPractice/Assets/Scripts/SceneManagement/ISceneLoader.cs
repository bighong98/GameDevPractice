

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace TH.SceneManagement
{
    public interface ISceneLoader
    {
        UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default);
        UniTask LoadSceneAsync(object key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default);
        event Func<CancellationToken, UniTask> OnBeforeSceneChanged;
        event Func<CancellationToken, UniTask> OnAfterSceneChanged;
        event Action<Scene> OnSceneChanged;
        
        IMessageBroadcaster<(float, string)> ProgressMessage { get; }
        IBroadcastSubscription SubscribeProgress(Action<(float, string)> onProgress);
    }
}


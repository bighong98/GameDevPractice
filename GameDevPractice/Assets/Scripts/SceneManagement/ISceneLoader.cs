

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
        
        // 씬 전환 이벤트
        event Func<CancellationToken, UniTask> OnBeforeSceneChanged;
        event Func<CancellationToken, UniTask> OnAfterSceneChanged;
        event Action<Scene> OnSceneChanged;
        // 씬 전환 오퍼레이션 게이트
        SceneTransitionGate CreateBeforeGate();
        SceneTransitionGate CreateAfterGate();
        
        IMessageBroadcaster<(float, string)> ProgressMessage { get; }
        IBroadcastSubscription SubscribeProgress(Action<(float, string)> onProgress);
    }
}


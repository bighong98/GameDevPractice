

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
        event Func<CancellationToken, UniTask> OnLastSceneChanged;
        event Action<Scene> OnSceneChanged;

        // 씬 전환 상태 프로퍼티
        bool IsLoadingScene { get; }
        SceneLoadingState LoadingState { get; }

        // 씬 전환 오퍼레이션 게이트
        SceneTransitionGate CreateBeforeGate();
        SceneTransitionGate CreateAfterGate();
        
        IMessageBroadcaster<(float, string)> ProgressMessage { get; }
        IBroadcastSubscription SubscribeProgress(Action<(float, string)> onProgress);
    }

    public enum SceneLoadingState
    {
        None,
        Initialized,
        OnBeforeSceneChanged,
        OnAfterSceneChanged,
        OnLastSceneChanged,
    }
}


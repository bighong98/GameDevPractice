

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace TH.SceneManagement
{
    public interface ISceneLoader
    {
        // progress 관련 매서드는 인터페이스 분리 고려
        IProgress<float> Progress { get; }
        void BindProgress(IProgress<float> reporter);
        void BindProgress(Action<float> onProgress);
        UniTask LoadSceneAsync(AssetReferenceScene sceneRef, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default);
        UniTask LoadSceneAsync(string key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default);
        event Action<Scene> OnSceneChanged;
    }
}


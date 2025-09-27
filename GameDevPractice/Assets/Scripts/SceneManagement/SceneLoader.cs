using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        private const string LoadingSceneName = "LoadingScene";
        private const float TempStepSize = 0.1f;
        private const float SceneLoadStartPoint = 0.6f;
        private const float SceneActivateStartPoint = 0.9f;

        private AsyncOperationHandle<SceneInstance> currentSceneHandle;
        private AsyncOperationHandle<SceneInstance> prevSceneHandle;

        private bool inFlight;
        public SceneLoader()
        {
            Init();
        }

        private void Init()
        {
            LoadLoadingSceneAsync().Forget();
        }

        private static async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var op = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
            while (!op.isDone)
            {
                token.ThrowIfCancellationRequested();
                onProgress?.Invoke(Mathf.Lerp(0f, 0.05f, op.progress));
                await UniTask.Yield(token);
            }
            onProgress?.Invoke(0.05f);
        }
        
        public UniTask LoadSceneAsync(AssetReferenceScene sceneRef, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default)
        {
            throw new System.NotImplementedException();
        }

        public async UniTask LoadSceneAsync(string key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} empty key");

            if (inFlight) return;
            inFlight = true; // 동시호출 방지(임시)

            try
            {
                await LoadLoadingSceneAsync(token: token);

                if (preTasks != null) // 씬 로드 전 사전 작업 실행
                {
                    int c = 0;
                    foreach (var task in preTasks)
                    {
                        token.ThrowIfCancellationRequested();
                        await (task?.Invoke(token) ?? UniTask.CompletedTask);
                        float ratio = Mathf.Lerp(0f, SceneLoadStartPoint, Mathf.Clamp01(++c * TempStepSize));
                        onProgress?.Invoke(ratio);
                    }
                    onProgress?.Invoke(SceneLoadStartPoint);
                }

                await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
                await UnloadPreviousSceneAsync(token); // 이전 씬 언로드
                onProgress?.Invoke(1f); // 진행도 100%
            }
            finally { inFlight = false; }
        }
        
        private async UniTask LoadSceneWithAddressablesAsync(string key, Action<float> onProgress = null,
            CancellationToken token = default)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);

                while (!handle.IsDone) // SceneLoadStartPoint: 0.9f, 진행도의 시각적 표현을 위한 임의의 기준점
                {
                    token.ThrowIfCancellationRequested();
                    float ratio = Mathf.Lerp(SceneLoadStartPoint, SceneActivateStartPoint, handle.PercentComplete);
                    onProgress?.Invoke(ratio);
                    await UniTask.Yield(token);
                }

                if (handle.Status == AsyncOperationStatus.Failed)
                    throw handle.OperationException ??
                          new Exception($"[{nameof(SceneLoader)}] load scene failed: {key}");

                var sceneInstance = handle.Result;
                var sceneOp = sceneInstance.ActivateAsync();
                var progress = new Progress<float>(x => { onProgress?.Invoke(Mathf.Lerp(SceneActivateStartPoint, 1.0f, x)); });
                await sceneOp.ToUniTask(progress: progress, cancellationToken: token);

                SceneManager.SetActiveScene(sceneInstance.Scene);
                
                // 이전 씬, 현재 씬 갱신
                if (currentSceneHandle.IsValid())
                    prevSceneHandle = currentSceneHandle;
                currentSceneHandle = handle;
                
                await UniTask.NextFrame(token); // 1 프레임 대기

                onProgress?.Invoke(SceneActivateStartPoint); // 진행도 90%로 전달 
            }
            catch
            {
                if (handle.IsValid())
                {
                    try
                    {
                        if (handle.IsDone)
                            await Addressables.UnloadSceneAsync(handle, true).ToUniTask(cancellationToken: token);
                        else
                            Addressables.Release(handle);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"[{nameof(SceneLoader)}]{e.Message}");
                    }
                }

                Util.LogError($"[{nameof(SceneLoader)}] load scene failed: {key}");
                throw;
            }
        }

        private async UniTask UnloadPreviousSceneAsync(CancellationToken token)
        {
            if (prevSceneHandle.IsValid())
            {
                try
                {
                    var prev = prevSceneHandle.Result;
                    await Addressables.UnloadSceneAsync(prev, autoReleaseHandle: true)
                        .ToUniTask(cancellationToken: token);
                }
                catch (Exception e) { Util.LogError($"[{nameof(SceneLoader)}] {nameof(UnloadPreviousSceneAsync)}: exception occured while unload scene '{prevSceneHandle.DebugName}' - {e}"); }
                finally { prevSceneHandle = default; }
            }
        }
    }
}



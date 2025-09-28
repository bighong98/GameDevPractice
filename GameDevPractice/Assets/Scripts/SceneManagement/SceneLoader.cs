using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
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
        private CancellationTokenSource cts = new CancellationTokenSource();
        
        public IProgress<float> Progress { get; private set; }
        
        public SceneLoader()
        {
            BindProgress(reporter: null); // 빈 객체로 초기화
            Init();
        }

        private async void Init()
        {
            await LoadSceneAsync("Sandbox");
        }

        private async UniTask WaitForPreLoad()
        {
            try
            {
                if (ServiceLocator.TryGet(out IResourceLoader resourceLoader))
                {
                    if (!resourceLoader.IsPreLoadDone())
                    {
                        Util.Log($"[SceneLoader] WaitForPreLoad");
                        resourceLoader.NotifyResourceLoad += OnPreloadDone;
                        while (!(cts?.IsCancellationRequested ?? true))
                        {
                            await UniTask.NextFrame();
                        }
                    }
                }
            }
            catch (Exception e) {Util.LogError($"{e}");}
            finally{ Util.Log($"WaitForPreLoad is done");}
        }

        private void OnPreloadDone(string label)
        {
            if (label != "PreLoad") return; // todo: fix hard code 
            
            if (!(cts?.IsCancellationRequested ?? true))
            {
                cts.Cancel();
            }
            cts?.Dispose();
        }

        #region Load/Unload Scene

        private async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                return;
            
            var op = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
            await op.ToUniTask(cancellationToken: token);
            await WaitForPreLoad();
        }

        private static async UniTask UnloadLoadingSceneAsync(CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                await SceneManager.UnloadSceneAsync(loadScene).ToUniTask(cancellationToken: token);
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
                LoadLoadingSceneAsync().Forget();

                if (preTasks != null) // 씬 로드 전 사전 작업 실행
                {
                    int c = 0;
                    foreach (var task in preTasks)
                    {
                        token.ThrowIfCancellationRequested();
                        await (task?.Invoke(token) ?? UniTask.CompletedTask);
                        float ratio = Mathf.Lerp(0f, SceneLoadStartPoint, Mathf.Clamp01(++c * TempStepSize));
                        ReportProgress(ratio);
                    }
                    ReportProgress(SceneLoadStartPoint);
                }
                
                await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
                await UnloadPreviousSceneAsync(token); // 이전 씬 언로드
                await UnloadLoadingSceneAsync(token); // 로딩 씬 언로드
                ReportProgress(1f); // 진행도 100%
            }
            catch (Exception e) {Util.Log($"exception occured while loadingScene '{key}', {e}");}
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
                    ReportProgress(ratio);
                    await UniTask.Yield(token);
                }

                if (handle.Status == AsyncOperationStatus.Failed)
                    throw handle.OperationException ??
                          new Exception($"[{nameof(SceneLoader)}] load scene failed: {key}");

                var sceneInstance = handle.Result;
                var sceneOp = sceneInstance.ActivateAsync();
                var progress = new Progress<float>(x => { ReportProgress(Mathf.Lerp(SceneActivateStartPoint, 1.0f, x)); });
                await sceneOp.ToUniTask(progress: progress, cancellationToken: token);
                
                // 이전 씬, 현재 씬 갱신
                if (currentSceneHandle.IsValid())
                    prevSceneHandle = currentSceneHandle;
                currentSceneHandle = handle;
                
                await UniTask.NextFrame(token); // 1 프레임 대기
            }
            catch
            {
                Util.Log($"[SceneLoader] exception occured while load scene with addressables '{handle.DebugName}'");
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
                    Util.Log($"[SceneLoader] scene '{prev.Scene.name}' is unloaded");
                }
                catch (Exception e) { Util.LogError($"[{nameof(SceneLoader)}] {nameof(UnloadPreviousSceneAsync)}: exception occured while unload scene '{prevSceneHandle.DebugName}' - {e}"); }
                finally { prevSceneHandle = default; }
            }
        }

        #endregion

        #region Progress handle

        public void BindProgress(IProgress<float> reporter)
        {
            Progress = reporter ?? new Progress<float>(_ => { });
        }

        public void BindProgress(Action<float> onProgress)
        {
            Progress = new Progress<float>(onProgress ?? (_ => { }));
        }
        
        private void ReportProgress(float p, Action<float> additive = null)
        {
            Progress.Report(p);
            additive?.Invoke(p);
        }

        #endregion

    }
}



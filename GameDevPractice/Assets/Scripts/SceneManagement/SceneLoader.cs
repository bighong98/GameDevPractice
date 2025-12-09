using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using TH.SaveLoad;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using TH.Utils;

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        private readonly IResourceLoader resourceLoader;
        
        private const string LoadingSceneName = "LoadingScene";
        private const float SceneLoadStartPoint = 0.3f;
        private const float SceneActivateStartPoint = 0.6f;

        private AsyncOperationHandle<SceneInstance> currentSceneHandle;
        private AsyncOperationHandle<SceneInstance> prevSceneHandle;
        
        private bool inFlight;
        private CancellationTokenSource cts = new CancellationTokenSource();
        
        public event Func<UniTask> OnBeforeSceneChanged;
        public event Action<Scene> OnSceneChanged;
        
        public SceneLoader(IResourceLoader resourceLoad)
        {
            resourceLoader = resourceLoad;
            OnBeforeSceneChanged = () => UniTask.CompletedTask; // 빈 객체로 초기화 (NRE 방지)
            Init();
        }

        private bool testing = false;
        private async void Init()
        {
            if (testing)
                await LoadSceneAsync("Sandbox");
        }

        #region PreLoad

        private async UniTask WaitForPreLoad()
        {
            try
            {
                if (!resourceLoader.IsLoadedAll(Constants.PreLoadLabel))
                {
                    Logg.Log($"[SceneLoader] WaitForPreLoad", Logg.LoggingMode.Completed);
                    resourceLoader.OnLabelResourcesLoadedAll += OnPreloadDone;
                    while (!(cts?.IsCancellationRequested ?? true))
                    {
                        await UniTask.NextFrame();
                    }
                }
            }
            catch (Exception e) {Logg.LogError($"{e}");}
            finally{ Logg.Log($"[SceneLoader] WaitForPreLoad is done", Logg.LoggingMode.Completed);}
        }

        private void OnPreloadDone(string label)
        {
            if (label != Constants.PreLoadLabel) return; 
            
            if (!(cts?.IsCancellationRequested ?? true))
                cts.Cancel();
            cts?.Dispose();
        }

        #endregion
        
        #region Load/Unload Scene

        private async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                return;
            
            var op = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Additive);
            
            await UniTask.WhenAll(
                op.ToUniTask(cancellationToken: token),
                WaitForPreLoad()
            );
        }

        private static async UniTask UnloadLoadingSceneAsync(CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                await SceneManager.UnloadSceneAsync(loadScene).ToUniTask(cancellationToken: token);
        }

        public async UniTask LoadSceneAsync(object key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null,
            Action<float> onProgress = null, CancellationToken token = default)
        {
            switch (key)
            {
                case string strKey when string.IsNullOrWhiteSpace(strKey):
                    throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} empty key");
                case AssetReference refKey:
                {
                    if (!refKey.IsValid() || !refKey.RuntimeKeyIsValid())
                    {
                        key = new AssetReference(refKey.AssetGUID);
                        if (key == null)
                            throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} invalid Asset Reference for scene");
                    }

                    break;
                }
            }

            if (inFlight) return;
            inFlight = true;

            try
            {
                await UniTask.WhenAll(LoadLoadingSceneAsync(token: token),
                    RunPreTasks(preTasks, token)); // 로딩 씬 로드, 타겟 씬 로드 전 사전 작업
                var result = await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
                await UniTask.WhenAll(UnloadPreviousSceneAsync(token),
                        UnloadLoadingSceneAsync(token),
                        OnBeforeSceneChanged!()); // 로딩 씬 언로드, 기존 씬 언로드
                

                ReportProgress(1); // 진행도 60%
                OnSceneChanged?.Invoke(result.Scene);
            }
            catch (Exception e) { Logg.Log($"exception occured while loadingScene '{key}', {e}"); }
            finally { inFlight = false; }
        }
        
        private async UniTask RunPreTasks(IEnumerable<Func<CancellationToken, UniTask>> preTasks, CancellationToken token)
        {
            if (preTasks == null) return;
            
            // 씬 로드 전 사전 작업 실행
            foreach (var task in preTasks)
            {
                token.ThrowIfCancellationRequested();
                await (task?.Invoke(token) ?? UniTask.CompletedTask);
            }
        }

        private async UniTask<SceneInstance> LoadSceneWithAddressablesAsync(object key, Action<float> onProgress = null,
            CancellationToken token = default)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);
                
                var result = await handle;
                if (handle.Status == AsyncOperationStatus.Failed)
                    throw handle.OperationException ??
                          new Exception($"[{nameof(SceneLoader)}] load scene failed: {key}");

                await result.ActivateAsync().ToUniTask(cancellationToken: token);
                
                // 이전 씬, 현재 씬 갱신
                if (currentSceneHandle.IsValid())
                    prevSceneHandle = currentSceneHandle;
                currentSceneHandle = handle;
            }
            catch
            {
                Logg.Log($"[SceneLoader] exception occured while load scene with addressables '{handle.DebugName}'");
                if (!handle.IsValid())
                {
                    Logg.LogError($"[{nameof(SceneLoader)}] scene handle is invalid: {key}");
                    throw;
                }
                
                try
                {
                    if (handle.IsDone)
                        await Addressables.UnloadSceneAsync(handle, true).
                            ToUniTask(cancellationToken: token);
                    else Addressables.Release(handle);
                }
                catch (Exception e) { 
                    throw new Exception($"[{nameof(SceneLoader)}] " +
                                                          $"failed to load scene - {e.Message}"); 
                }
            }
            
            await UniTask.NextFrame(token); // 1 프레임 대기
            return handle.Result;
        }

        private async UniTask UnloadPreviousSceneAsync(CancellationToken token)
        {
            if (!prevSceneHandle.IsValid()) return;
            
            try
            {
                // OnBeforeSceneChanged?.Invoke(); // 기존 씬 언로드 전에 정리작업 실행
                var prev = prevSceneHandle.Result;
                await Addressables.UnloadSceneAsync(prev, autoReleaseHandle: true)
                    .ToUniTask(cancellationToken: token);
                Logg.Log($"[SceneLoader] scene '{prev.Scene.name}' is unloaded");
            }
            catch (Exception e)
            {
                Logg.LogError(
                    $"[{nameof(SceneLoader)}] {nameof(UnloadPreviousSceneAsync)}: exception occured while unload scene '{prevSceneHandle.DebugName}' - {e}");
            }
            finally
            {
                prevSceneHandle = default; 
                Progress.Clear();
            }
        }

        #endregion

        #region Progress handle
        
        public IProgressBroadcaster Progress { get; } = new ProgressBroadcaster();

        public IProgressSubscription SubscribeProgress(Action<float> onProgress)
        {
            return Progress.Subscribe(onProgress);
        }
        
        private void ReportProgress(float p, Action<float> additive = null)
        {
            Progress?.Report(p);
            additive?.Invoke(p);
            Logg.Log($"[SceneLoader] progress: {p}", Logg.LoggingMode.InProgress);
        }

        #endregion
    }
}



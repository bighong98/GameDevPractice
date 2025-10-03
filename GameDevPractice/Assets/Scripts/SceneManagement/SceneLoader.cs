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

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        private const string LoadingSceneName = "LoadingScene";
        private const float SceneLoadStartPoint = 0.3f;
        private const float SceneActivateStartPoint = 0.6f;

        private AsyncOperationHandle<SceneInstance> currentSceneHandle;
        private AsyncOperationHandle<SceneInstance> prevSceneHandle;
        
        private bool inFlight;
        private CancellationTokenSource cts = new CancellationTokenSource();
        
        public IProgress<float> Progress { get; private set; }
        public event Func<UniTask> OnBeforeSceneChanged;
        public event Action<Scene> OnSceneChanged;
        
        public SceneLoader()
        {
            BindProgress(reporter: null); // 빈 객체로 초기화
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
                if (ServiceLocator.TryGet(out IResourceLoader resourceLoader))
                {
                    if (!resourceLoader.IsPreLoadDone())
                    {
                        Util.Log($"[SceneLoader] WaitForPreLoad", Util.LoggingMode.Completed);
                        resourceLoader.NotifyResourceLoad += OnPreloadDone;
                        while (!(cts?.IsCancellationRequested ?? true))
                        {
                            await UniTask.NextFrame();
                        }
                    }
                }
            }
            catch (Exception e) {Util.LogError($"{e}");}
            finally{ Util.Log($"[SceneLoader] WaitForPreLoad is done", Util.LoggingMode.Completed);}
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

        #endregion
        
        #region Load/Unload Scene

        private async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                return;
            
            var op = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
            
            await UniTask.WhenAll(
                op.ToUniTask(cancellationToken: token), 
                WaitForPreLoad(), 
                OnBeforeSceneChanged()
                );
        }

        private static async UniTask UnloadLoadingSceneAsync(CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                await SceneManager.UnloadSceneAsync(loadScene).ToUniTask(cancellationToken: token);
        }
        

        // public UniTask LoadSceneAsync(AssetReferenceScene sceneRef, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null, CancellationToken token = default)
        // {
        //     return UniTask.CompletedTask;
        // }

        public async UniTask LoadSceneAsync(object key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null,
            Action<float> onProgress = null, CancellationToken token = default)
        {
            if (key is string strKey)
            {
                if (string.IsNullOrWhiteSpace(strKey))
                    throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} empty key");
            }
            else if (key is AssetReference refKey)
            {
                if (!refKey.IsValid() || !refKey.RuntimeKeyIsValid())
                    throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} invalid AssetReferenceScene key '{refKey}'");
            }

            if (inFlight) return;
            inFlight = true;

            try
            {
                await UniTask.WhenAll(LoadLoadingSceneAsync(token: token),
                    RunPreTasks(preTasks, token)); // 로딩 씬 로드, 타겟 씬 로드 전 사전 작업
                var result = await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
                await UniTask.WhenAll(UnloadPreviousSceneAsync(token),
                    UnloadLoadingSceneAsync(token)); // 로딩 씬 언로드, 기존 씬 언로드

                ReportProgress(1); // 진행도 60%
                OnSceneChanged?.Invoke(result.Scene);
            }
            catch (Exception e) { Util.Log($"exception occured while loadingScene '{key}', {e}"); }
            finally { inFlight = false; }
        }

        // public async UniTask LoadSceneAsync(string key, IEnumerable<Func<CancellationToken, UniTask>> preTasks = null, Action<float> onProgress = null,
        //     CancellationToken token = default)
        // {
        //     if (string.IsNullOrWhiteSpace(key))
        //         throw new ArgumentException($"[{nameof(SceneLoader)}] {nameof(LoadSceneAsync)} empty key");
        //
        //     if (inFlight) return;
        //     inFlight = true; // 동시호출 방지(임시)
        //
        //     try
        //     {
        //         await UniTask.WhenAll(LoadLoadingSceneAsync(token: token), RunPreTasks(preTasks, token)); // 로딩 씬 로드, 타겟 씬 로드 전 사전 작업
        //         var result = await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
        //         await UniTask.WhenAll(UnloadPreviousSceneAsync(token), UnloadLoadingSceneAsync(token)); // 로딩 씬 언로드, 기존 씬 언로드
        //         
        //         ReportProgress(1); // 진행도 60%
        //         OnSceneChanged?.Invoke(result.Scene);
        //     }
        //     catch (Exception e) {Util.Log($"exception occured while loadingScene '{key}', {e}");}
        //     finally { inFlight = false; }
        // }

        private async UniTask RunPreTasks(IEnumerable<Func<CancellationToken, UniTask>> preTasks, CancellationToken token)
        {
            if (preTasks != null) // 씬 로드 전 사전 작업 실행
            {
                foreach (var task in preTasks)
                {
                    token.ThrowIfCancellationRequested();
                    await (task?.Invoke(token) ?? UniTask.CompletedTask);
                }
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
            
            await UniTask.NextFrame(token); // 1 프레임 대기
            return handle.Result;
        }

        private async UniTask UnloadPreviousSceneAsync(CancellationToken token)
        {
            if (prevSceneHandle.IsValid())
            {
                try
                {
                    OnBeforeSceneChanged?.Invoke(); // 기존 씬 언로드 전에 정리작업 실행
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
            Util.Log($"[SceneLoader] progress: {p}");
        }

        #endregion

        public bool TryGetCurrentSceneEntry(out SceneEntry entry)
        {
            if (currentSceneHandle.IsValid())
            {
                
            }

            entry = null;
            return false;
        }
    }
}



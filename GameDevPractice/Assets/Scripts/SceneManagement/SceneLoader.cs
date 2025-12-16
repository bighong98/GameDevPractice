using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        private readonly IResourceLoader resourceLoader;

        private AsyncOperationHandle<SceneInstance> currentSceneHandle;
        private AsyncOperationHandle<SceneInstance> prevSceneHandle;
        
        private bool inFlight;
        
        public event Func<UniTask> OnBeforeSceneChanged;
        public event Action<Scene> OnSceneChanged;
        
        private const string LoadingSceneName = "LoadingScene";
        private const float SceneLoadStartPoint = 0.3f;
        private const float SceneActivateStartPoint = 0.6f;
        
        public SceneLoader(IResourceLoader resourceLoad)
        {
            resourceLoader = resourceLoad;
            Init();
        }
        
#if UNITY_EDITOR
        Scene bootScene;
        bool bootSceneUnLoaded;
#endif
        
        private void Init()
        {
            OnBeforeSceneChanged = () => UniTask.CompletedTask; // 빈 객체로 초기화 (NRE 방지)
#if UNITY_EDITOR
            bootScene = SceneManager.GetActiveScene();
            bootSceneUnLoaded = false;
#endif
        }

        #region PreLoad (using IResourceLoader)
        
        private async UniTask WaitForPreLoad(CancellationToken token = default)
        {
            if (resourceLoader.IsLoadedAll(Constants.PreLoadLabel))
                return;

            SubscribePreLoadProgress(Constants.PreLoadLabel);
            
            var tcs = new UniTaskCompletionSource();
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, () => tcs.TrySetResult());

            using (token.Register(() => tcs.TrySetCanceled(token)))
            {
                await tcs.Task;
            }
        }

        private void SubscribePreLoadProgress(string label)
        {
            resourceLoader.SubscribePreLoadProgress(label, ReportingProgressAction);
        }

        void ReportingProgressAction(float f)
        {
            float mapped = Mathf.Lerp(SceneLoadStartPoint, SceneActivateStartPoint, f);
            Logg.Log($"[{GetType().Name}] Progress({mapped})", Logg.LoggingMode.InProgress);
            ReportProgress(mapped);
        }

        #endregion
        
        #region Load/Unload Scene

        private async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && loadScene.isLoaded)
                return;
            
            await SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Additive).ToUniTask(cancellationToken: token);
#if UNITY_EDITOR
            await UnloadBootstrapSceneIfNeeded(token);
#endif
            await WaitForPreLoad(token);
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
                await UniTask.WhenAll(
                    LoadLoadingSceneAsync(token: token),
                    RunPreTasks(preTasks, token),
                    OnBeforeSceneChanged!()
                ); // 로딩 씬 로드, 타겟 씬 로드 전 사전 작업
                var result = await LoadSceneWithAddressablesAsync(key, onProgress, token); // 타겟 씬 로드
                await UniTask.WhenAll(
                    UnloadPreviousSceneAsync(token),
                    UnloadLoadingSceneAsync(token)
                ); // 로딩 씬 언로드, 기존 씬 언로드
                ReportProgress(1); // 진행도 60%
                await result.ActivateAsync().ToUniTask(cancellationToken: token);
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
    
        #region Helper Methods
#if UNITY_EDITOR
        // 에디터 환경 버그 방지
        // Play 버튼 누른 시점의 에디터 상 활성 씬 언로드
        private async UniTask UnloadBootstrapSceneIfNeeded(CancellationToken token)
        {
            if (bootSceneUnLoaded)
                return;

            if (!bootScene.IsValid())
            {
                bootSceneUnLoaded = true;
                return;
            }

            // 혹시나 부트씬이 LoadingScene인 경우는 건너뜀
            if (bootScene.name == LoadingSceneName)
            {
                bootSceneUnLoaded = true;
                return;
            }

            await SceneManager.UnloadSceneAsync(bootScene)
                .ToUniTask(cancellationToken: token);

            Logg.Log($"[SceneLoader] bootstrap scene '{bootScene.name}' is unloaded");

            bootSceneUnLoaded = true;
        }
#endif

        #endregion
    }
}



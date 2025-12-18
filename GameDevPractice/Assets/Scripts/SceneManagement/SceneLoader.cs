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
        
        public event Func<CancellationToken, UniTask> OnBeforeSceneChanged;
        public event Func<CancellationToken, UniTask> OnAfterSceneChanged;
        public event Action<Scene> OnSceneChanged;
        
        private const string LoadingSceneName = "LoadingScene";
        // PreLoad 완료 후 시작점 (ResourceLoader에서 0~0.7 범위로 진행도 보고)
        private const float PostPreLoadStartPoint = 0.7f;
        
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
            // 이벤트 내부 빈 객체로 초기화 (NRE 방지)
            OnBeforeSceneChanged = (token) => UniTask.CompletedTask; 
            OnAfterSceneChanged = (token) => UniTask.CompletedTask; 
#if UNITY_EDITOR
            bootScene = SceneManager.GetActiveScene();
            bootSceneUnLoaded = false;
#endif
            // 전체 PreLoad 진행도 구독
            SubscribeGlobalPreLoadProgress();
        }

        #region PreLoad (using IResourceLoader)
        
        private async UniTask WaitForPreLoad(CancellationToken token = default)
        {
            // Init()에서 이미 구독했으므로 여기서는 로드 완료만 대기
            if (resourceLoader.IsLoadedAll(Constants.PreLoadLabel))
                return;

            var tcs = new UniTaskCompletionSource();
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, () => tcs.TrySetResult());

            using (token.Register(() => tcs.TrySetCanceled(token)))
            {
                await tcs.Task;
            }
        }

        private void SubscribeGlobalPreLoadProgress()
        {
            resourceLoader.SubscribeGlobalPreLoadProgress(ReportingProgressAction);
        }

        void ReportingProgressAction(float globalProgress)
        {
            ReportProgress(globalProgress);
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
            float prevTimeScale = Time.timeScale;

            try
            {
                // 로딩 씬 로드(최초 1회)
                await LoadLoadingSceneAsync(token: token);
                // 타겟 씬 비동기 로드 
                var result = await LoadSceneWithAddressablesAsync(key, onProgress, token);
                // 씬 전환 전 사전작업 처리
                await UniTask.WhenAll(
                    OnBeforeSceneChanged.InvokeAllThrottledAsync(token),
                    RunPreTasks(preTasks, token)
                );
                // 타겟 씬 활성화
                await result.ActivateAsync().ToUniTask(cancellationToken: token);
                Time.timeScale = 0f; // 게임 시간 일시정지 (todo: timeScale 대신 게임 플레이 일시정지 기능 추가하여 대체)
                // 씬 매니저에게 Active Scene 변동 전달 (멀티 씬 문제 대응)
                SceneManager.SetActiveScene(result.Scene);
                ReportProgress(1); // 진행도 100% 전달

                // 이전 씬 언로드 및 씬 전환 이벤트 호출
                await UniTask.WhenAll(
                    UnloadPreviousSceneAsync(token),
                    OnAfterSceneChanged.InvokeAllThrottledAsync(token)
                );
                OnSceneChanged?.Invoke(result.Scene);
            }
            catch (Exception e)
            {
                Logg.Log($"exception occured while loadingScene '{key}', {e}");
            }
            finally
            {
                inFlight = false;
                Time.timeScale = prevTimeScale;
            }
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
                Logg.Log($"[SceneLoader] LoadSceneWithAddressablesAsync({key})", Logg.LoggingMode.Completed);
                handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);
                
                var result = await handle;
                if (handle.Status == AsyncOperationStatus.Failed)
                    throw handle.OperationException ??
                          new Exception($"[{nameof(SceneLoader)}] load scene failed: {key}");
                
                // await result.ActivateAsync().ToUniTask(cancellationToken: token);
                
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
                catch (Exception e) { throw new Exception($"[{nameof(SceneLoader)}] " +
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
                var prevSceneName = prev.Scene.name; // 디버깅용 씬 이름 클로저
                await Addressables.UnloadSceneAsync(prevSceneHandle, autoReleaseHandle: true)
                    .ToUniTask(cancellationToken: token);
                Logg.Log($"[SceneLoader] scene '{prevSceneName}' is unloaded", Logg.LoggingMode.InProgress);
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
            Logg.Log($"[SceneLoader] progress: {p}", Logg.LoggingMode.Completed);
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



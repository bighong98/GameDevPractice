using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

using TH.SceneManagement.Data;
using TH.Utils;
using TH.Resource;

namespace TH.SceneManagement
{
    public class SceneLoader : ISceneLoader
    {
        // 씬 전환 이벤트
        public event Func<CancellationToken, UniTask> OnBeforeSceneChanged;
        public event Func<CancellationToken, UniTask> OnAfterSceneChanged;
        public event Func<CancellationToken, UniTask> OnLastSceneChanged;
        public event Action<Scene> OnSceneChanged;
        
        // 외부 서비스, 리소스
        private readonly IResourceLoader resourceLoader;
        private SceneCatalogSO sceneCatalog;
        
        // 현재/이전 씬 핸들 (현재 씬 상태 확인 및 이전 씬 언로드에 사용)
        private AsyncOperationHandle<SceneInstance> currentSceneHandle;
        private AsyncOperationHandle<SceneInstance> prevSceneHandle;
       
        // 씬 전환 중 확인 플래그
        private bool isLoadingScene;
        public bool IsLoadingScene => isLoadingScene;
        public SceneLoadingState LoadingState { get; private set; } = SceneLoadingState.None;
        
        // 리소스 어드레서블 키
        private const string LoadingSceneName = "LoadingScene";
        private const string SceneCatalogKey =  "SceneCatalogSO";
        
        
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
            OnBeforeSceneChanged = _ => UniTask.CompletedTask; 
            OnAfterSceneChanged = _ => UniTask.CompletedTask;
            OnLastSceneChanged = _ => UniTask.CompletedTask;
#if UNITY_EDITOR
            // 에디터 환경일 경우 플레이 시점 씬 캐시
            bootScene = SceneManager.GetActiveScene();
            bootSceneUnLoaded = false;
#endif
            // 전체 PreLoad 진행도 구독
            SubscribeGlobalPreLoadProgress();
        }

        #region PreLoad (using IResourceLoader)
        
        private async UniTask WaitForPreLoad(CancellationToken token = default)
        {
            // PreLoad가 완료되지 않은 경우 대기
            if (!resourceLoader.IsLoadedAll(Constants.PreLoadLabel))
            {
                var tcs = new UniTaskCompletionSource();
                resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, () => tcs.TrySetResult());

                using (token.Register(() => tcs.TrySetCanceled(token)))
                {
                    await tcs.Task;
                }
            }
            
            // PreLoad 완료 여부와 상관없이 sceneCatalog가 없으면 로드
            if (sceneCatalog == null)
            {
                await LoadSceneCatalogAsync(token);
            }
        }

        private async UniTask LoadSceneCatalogAsync(CancellationToken token)
        {
            sceneCatalog = await resourceLoader.LoadAsync<SceneCatalogSO>(SceneCatalogKey, token);
        }

        private void SubscribeGlobalPreLoadProgress()
        {
            resourceLoader.SubscribeGlobalPreLoadProgress(ReportingProgressAction);
        }

        void ReportingProgressAction((float, string) globalProgress)
        {
            ReportProgress(globalProgress, format: ProgressTextFormat.LoadingAssetsIn);
        }

        #endregion
        
        #region Load/Unload Scene

        public async UniTask LoadLoadingSceneAsync(Action<float> onProgress = null, CancellationToken token = default)
        {
            var loadScene = SceneManager.GetSceneByName(LoadingSceneName);
            if (loadScene.IsValid() && SceneManager.GetActiveScene() == loadScene || loadScene.isLoaded)
                return;
            
            this.Log($"loadScene: {loadScene}, IsValid: {loadScene.IsValid()}, isLoaded: {loadScene.isLoaded}", Logg.LoggingMode.Completed);
            
            await SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Additive).ToUniTask(cancellationToken: token);
#if UNITY_EDITOR
            await UnloadBootstrapSceneIfNeeded(token);
#endif
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

            if (isLoadingScene) return;
            isLoadingScene = true;
            LoadingState = SceneLoadingState.Initialized;

            float prevTimeScale = Time.timeScale;
            BeginTransition(); // 씬 전환 
            
            try
            {
                // 로딩 씬 로드(최초 1회)
                // 리소스 일괄 로드 대기 (최초 1회)
                await UniTask.WhenAll(
                    LoadLoadingSceneAsync(token: token),
                    WaitForPreLoad(token)
                );
                
                // 진행도 70% + 현재 씬 이름 전달 (SceneCatalogSO로 선 조회)
                var sceneName = GetSceneNameFromKey(key);
                this.Log($"sceneName: {sceneName}", Logg.LoggingMode.Completed);
                ReportProgress((0.7f, sceneName), format: ProgressTextFormat.LoadingScene);
                // 타겟 씬 비동기 로드 
                var result = await LoadSceneWithAddressablesAsync(key, onProgress, token);
                // 씬 전환 전 사전작업 처리
                this.Log($"OnBeforeSceneChanged starts - scene: {result.Scene.name}", Logg.LoggingMode.Completed);
                LoadingState = SceneLoadingState.OnBeforeSceneChanged;
                await UniTask.WhenAll(
                    OnBeforeSceneChanged.InvokeAllThrottledAsync(token),
                    AwaitBeforeGates(token),
                    RunPreTasks(preTasks, token)
                );
                CloseBeforePhase();
                // 진행도 100% 전달
                ReportProgress((0.8f, sceneName), format: ProgressTextFormat.LoadingScene);
                // 타겟 씬 활성화 (씬에 배치된 게임오브젝트이ㅡ Awake(), OnEnable() 실행됨)
                await result.ActivateAsync().ToUniTask(cancellationToken: token);
                // 씬 매니저에게 Active Scene 변동 전달 (멀티 씬 문제 대응)
                SceneManager.SetActiveScene(result.Scene);
                // 1프레임 대기 (Start() 실행 보장 
                // -> SceneManager.GetActiveScene()으로 현재 활성화 씬 정보가 필요한 작업의 경우 Start에서 실행 권장함)
                await UniTask.Yield();
                
                // 게임 시간 일시정지 // todo: timeScale 대신 게임 플레이 일시정지 기능 추가하여 대체
                Time.timeScale = 0f;
                // 진행도 100% 전달
                ReportProgress((1f, "Loading ended. Wait for seconds")); 
                
                // 이전 씬 언로드 및 씬 전환 이벤트 호출
                // 씬 언로드와 함께 실행되는 이벤트 메서드(OnDestroy, etc)가 호출되는 시점엔 이미 활성 씬이 바뀐 상태임에 주의
                this.Log($"OnAfterSceneChanged starts - scene: {result.Scene.name}", Logg.LoggingMode.Completed);
                LoadingState = SceneLoadingState.OnAfterSceneChanged;
                await UniTask.WhenAll(
                    UnloadPreviousSceneAsync(token),
                    OnAfterSceneChanged.InvokeAllThrottledAsync(token),
                    AwaitAfterGates(token)
                );
                CloseAfterPhase();
                OnSceneChanged?.Invoke(result.Scene);
                
                // 씬 전환 후 최종 예약 작업 실행 (씬 전환 연출 포함)
                LoadingState = SceneLoadingState.OnLastSceneChanged;                
                await UniTask.WhenAll(
                    OnLastSceneChanged.InvokeAllThrottledAsync(token),
                    UniTask.DelayFrame(60, cancellationToken: token)
                );
            }
            catch (Exception e)
            {
                Logg.LogWarning($"exception occured while loadingScene '{key}', {e}");
            }
            finally
            {
                // 씬 전환 관련 플래그 갱신 및 게임 일시정지 해제
                isLoadingScene = false;
                LoadingState = SceneLoadingState.None;
                Time.timeScale = prevTimeScale;
                EndTransitionInvalidateAll();
            }
        }
        
        string GetSceneNameFromKey(object key)
        {
            if (key is AssetReference { AssetGUID: { Length: > 0 } sceneGuid } 
                && sceneCatalog.FindByGuid(sceneGuid) is { key: { Length: > 0 } targetSceneName }) 
                return targetSceneName;
            return string.Empty;
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
                
                // // 씬 활성화는 LoadSceneAsync()에서 씬 활성화 전 수행이 필요한 작업(정리 작업) 처리 후 실행
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
                        await Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true).
                            ToUniTask(cancellationToken: token);
                    else Addressables.Release(handle);
                }
                catch (Exception e) { throw new Exception($"[{nameof(SceneLoader)}] " 
                                                          + $"failed to load scene - {e.Message}"); }
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
                Logg.Log($"[SceneLoader] scene '{prevSceneName}' is unloaded", Logg.LoggingMode.Completed);
            }
            catch (Exception e)
            {
                Logg.LogError(
                    $"[{nameof(SceneLoader)}] {nameof(UnloadPreviousSceneAsync)}: exception occured while unload scene '{prevSceneHandle.DebugName}' - {e}");
            }
            finally
            {
                prevSceneHandle = default; 
                ProgressMessage.Clear();
            }
        }

        #endregion

        #region Progress handle
        
        public IMessageBroadcaster<(float, string)> ProgressMessage { get; } = new MessageBroadcaster<(float, string)>();

        public IBroadcastSubscription SubscribeProgress(Action<(float, string)> onProgress)
        {
            return ProgressMessage.Subscribe(onProgress);
        }
        
        private void ReportProgress((float, string) p, Action<(float, string)> additive = null,
            ProgressTextFormat format = ProgressTextFormat.Raw)
        {
            ProgressMessage?.Report((p.Item1, FormatProgressText(p.Item2, format)));
            additive?.Invoke((p.Item1, FormatProgressText(p.Item2, format)));
            this.Log($"ReportProgress: {(p.Item1, FormatProgressText(p.Item2, format))}", Logg.LoggingMode.Completed);
        }
        
        public enum ProgressTextFormat
        {
            LoadingAssetsIn,     // "loading assets in {raw}..."
            LoadingScene,        // "loading scene: {raw}..."
            UnloadingScene,      // "unloading scene: {raw}..."
            PreloadingLabel,     // "preloading label: {raw}..."
            Initializing,        // "initializing: {raw}..."
            Raw              // "{raw}"
        }

        private readonly StringBuilder _sb = new StringBuilder(64);

        private string FormatProgressText(string raw, ProgressTextFormat format)
        {
            _sb.Clear();

            // null 방어 (원하시면 string.Empty 대신 고정 문구로 바꿔도 됨)
            raw ??= string.Empty;

            switch (format)
            {
                case ProgressTextFormat.LoadingAssetsIn:
                    _sb.Append("리소스 로드 중 (");
                    _sb.Append(raw);
                    _sb.Append(")...");
                    break;

                case ProgressTextFormat.LoadingScene:
                    _sb.Append("씬 로딩 중 (");
                    _sb.Append(raw);
                    _sb.Append(")...");
                    break;

                case ProgressTextFormat.UnloadingScene:
                    _sb.Append("씬 언로드 중 (");
                    _sb.Append(raw);
                    _sb.Append(")...");
                    break;

                case ProgressTextFormat.PreloadingLabel:
                    _sb.Append("리소스 로드 중 (label: ");
                    _sb.Append(raw);
                    _sb.Append(")...");
                    break;

                case ProgressTextFormat.Initializing:
                    _sb.Append("시작 중 (");
                    _sb.Append(raw);
                    _sb.Append(")...");
                    break;

                case ProgressTextFormat.Raw:
                default:
                    _sb.Append(raw);
                    break;
            }

            return _sb.ToString();
        }

        #endregion

        #region Scene Transition Gate

        private int _transitionId; // 0이면 전환 없음
        private bool _beforeOpen;
        private bool _afterOpen;

        private readonly List<SceneTransitionGate> _beforeGates = new();
        private readonly List<SceneTransitionGate> _afterGates  = new();

        public SceneTransitionGate CreateBeforeGate()
        {
            if (_transitionId == 0 || !_beforeOpen)
                return SceneTransitionGate.CreateCompletedNoop();

            var g = new SceneTransitionGate(_transitionId);
            _beforeGates.Add(g);
            return g;
        }

        public SceneTransitionGate CreateAfterGate()
        {
            if (_transitionId == 0 || !_afterOpen)
                return SceneTransitionGate.CreateCompletedNoop();

            var g = new SceneTransitionGate(_transitionId);
            _afterGates.Add(g);
            return g;
        }

        private void BeginTransition()
        {
            _transitionId++;
            _beforeOpen = true;
            _afterOpen = true;

            _beforeGates.Clear();
            _afterGates.Clear();
        }

        private void CloseBeforePhase() => _beforeOpen = false;
        private void CloseAfterPhase() => _afterOpen = false;

        // 전환이 끝나면 전부 무효화 + 게이트 목록 제거
        private void EndTransitionInvalidateAll()
        {
            foreach (var t in _beforeGates)
                t.Invalidate(_transitionId);

            foreach (var t in _afterGates)
                t.Invalidate(_transitionId);

            _beforeGates.Clear();
            _afterGates.Clear();

            _beforeOpen = false;
            _afterOpen = false;
        }

        private UniTask AwaitBeforeGates(CancellationToken token)
        {
            if (_beforeGates.Count == 0) return UniTask.CompletedTask;
            
            var snapshot = _beforeGates.ToArray();
            var tid = _transitionId;

            using (token.Register(() =>
               {
                   // 취소되면 전부 cancel
                   foreach (var t in snapshot)
                       t.TryCancel(tid, token);
               }))
            {
                var tasks = new UniTask[snapshot.Length];
                for (int i = 0; i < snapshot.Length; i++)
                    tasks[i] = snapshot[i].Task;

                return UniTask.WhenAll(tasks);
            }
        }

        private UniTask AwaitAfterGates(CancellationToken token)
        {
            if (_afterGates.Count == 0) return UniTask.CompletedTask;

            var snapshot = _afterGates.ToArray();
            var tid = _transitionId;

            using (token.Register(() =>
                   {
                       // 취소되면 전부 cancel
                       foreach (var t in snapshot)
                           t.TryCancel(tid, token);
                   }))
            {
                var tasks = new UniTask[snapshot.Length];
                for (int i = 0; i < snapshot.Length; i++)
                    tasks[i] = snapshot[i].Task;

                return UniTask.WhenAll(tasks);
            }
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

        #region Full Screen Effect

        private static readonly int FullScreenFeatureHash = URPFeatureHandler.StringToHash("FullScreenPassRendererFeature");

        private void DisableFullScreenEffect()
        {
            URPFeatureHandler.SetFeatureActive<FullScreenPassRendererFeature>(FullScreenFeatureHash, false);
        }

        private void EnableFullScreenEffect()
        {
            URPFeatureHandler.SetFeatureActive<FullScreenPassRendererFeature>(FullScreenFeatureHash, true);
        }

        #endregion
    }
}



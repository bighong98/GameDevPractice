using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine;

namespace TH.Resource
{
    // 어드레서블 기반 리소스 관리 서비스 클래스
    // 로드가 완료된 에셋의 operationHandle을 보관
    // handle을 통해 어드레서블로부터 리소스 참조 반환 및 메모리 언로드 여부 관리
    // 외부에서 key(string)/AssetReference로 리소스 접근 가능
    public class ResourceLoader : IResourceLoader
    {
        // Addressables.LoadAssetAsync 결과 핸들 캐시 (키 기반)
        private readonly Dictionary<string, AsyncOperationHandle> resourceKeys = new();
        // Addressables.LoadAssetAsync 결과 핸들 캐시 (AssetReference 기반)
        private readonly Dictionary<string, AsyncOperationHandle> resourceGuids = new ();
        // 라벨별 에셋 번들 로드 상태 추적
        private readonly Dictionary<string, LoadStatus> loadStatus = new Dictionary<string, LoadStatus>();
        // 라벨 단위로 일괄 리소스 로드 완료 알림 이벤트
        public event Action<string> OnLabelResourcesLoadedAll; 
        private readonly Dictionary<string, Queue<Action>> reservedPreLoadTasks = new();
        
        #region Enums
        // 어드레서블 프리로드 라벨
        // 반드시 동일한 이름의 어드레서블 라벨이 존재해야함
        enum PreLoadLabels
        {
            PreLoad_First, // 구분 무시하고 가장 처음에 로드해야할 리소스
            PreLoad_Asset, // 일반 에셋 (AudioClip, Texture, Material, etc)
            PreLoad_DataSO, // 데이터 컨테이너 Scriptable Object
            PreLoad_CatalogSO, // DataSO의 목록(AssetReference 형태)을 가지고 있는 Scriptable Object
            PreLoad_Prefab, // 프리팹 (내부 필드로 AssetReference 타입이 없는 경우 Asset으로 둬도 무방)
            PreLoad_Last, // 가장 마지막에 로드할 필요가 있는 리소스
        }
        // 라벨별 로드 상태
        enum LoadStatus
        {
            NotInitialized,
            InProgress,
            Done,
            Fail,
        }

        #endregion

        public ResourceLoader()
        {
            Init();  
        }

        #region Initialization

        private void Init()
        {
            foreach (var label in Enum.GetNames(typeof(PreLoadLabels)))
            {
                InitForLabel(label);
            }
        }
        
        private void InitForLabel(string label)
        {
            loadStatus[label] = LoadStatus.NotInitialized;
            reservedPreLoadTasks[label] = new Queue<Action>();

            InitLabelProcess(label);
        }

        #endregion

        #region PreLoad

        // 어플리케이션 시작 시점에 라벨 단위로 구분된 에셋 번들 로드
        // 라벨별로 로드 완료 시 이벤트 전달
        // 개별 리소스 로드마다 콜백 실행 (로딩 프로그레스 바 등에 사용)
        public async UniTask PreLoadAsync()
        {
            foreach (var label in Enum.GetNames(typeof(PreLoadLabels)))
            {
                Logg.Log($"[ResourceLoader] start to load label '{label}' assets", Logg.LoggingMode.Completed);
                await LoadAllAsync<UnityEngine.Object>(label);
            }
        }

        // 특정 어드레서블 라벨 로드까지 대기 콜백 등록
        // 이미 완료된 리소스 라벨인 경우 즉시 콜백 실행
        public void WaitForPreLoad(string label, Action callback)
        {
            if (IsLoadedAll(label))
            {
                callback?.Invoke();
                return;
            }
            
            if (!reservedPreLoadTasks.TryGetValue(label, out var queue))
            {
                Logg.LogWarning($"[ResourceLoader] invalid preload label accepted");
                return;
            }
            
            queue.Enqueue(callback);
        }

        private void NotifyPreLoadDone(string label)
        {
            RunReserved(label);
            OnLabelResourcesLoadedAll?.Invoke(label);
        }
        // 특정 어드레서블 라벨에 소속된 리소스 일괄 로드가 끝난 경우 대기 중인 작업들 실행
        private void RunReserved(string label)
        {
            if (!reservedPreLoadTasks.TryGetValue(label, out var queue) || queue.Count <= 0) return;
            while (queue.TryDequeue(out var task))
            {
                task?.Invoke();
            }
        }

        #endregion

        #region Load (Async)
        
        // 라벨에 매칭되는 모든 리소스 로케이션을 조회한 뒤, 각 항목을 비동기 로드
        // 개별 리소스 로드가 완료될 때마다 콜백(진행도 확인 목적)
        private async UniTask LoadAllAsync<T>(string label, Action<string, int, int> callback = null, CancellationToken token = default)
            where T : UnityEngine.Object
        {
            var locationHandles = Addressables.LoadResourceLocationsAsync(label, typeof(T));
            var locations = await locationHandles.ToUniTask(cancellationToken: token);

            int totalCount = locations.Count;
            int loadCount = 0;

            // 리소스가 없는 라벨은 즉시 100% 완료 처리
            if (totalCount == 0)
            {
                ReportPreLoadProgress(label, 1f);
            }

            var tasks = new List<UniTask>(totalCount);

            foreach (var loc in locations)
            {
                string key = loc.PrimaryKey;
                if (resourceKeys.ContainsKey(key))
                {
                    loadCount++;
                    continue;
                }

                var handle = Addressables.LoadAssetAsync<T>(loc);
                resourceKeys[key] = handle;
                
                tasks.Add(LoadAndInitAsync(handle, async asset =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    loadCount++;
                    callback?.Invoke(key, loadCount, totalCount);
                    ReportPreLoadProgress(label, (totalCount <= 0) ? 1f : (loadCount / (float)totalCount));
                    await DoAsyncInitialize(asset, token);
                    Logg.Log($"[ResourceLoader] {label} - finished loading {key}:{handle.Result} ({loadCount}/{totalCount}, {(loadCount / (float)totalCount)})", 
                        Logg.LoggingMode.Completed);
                }, token));
            }
            await UniTask.WhenAll(tasks);
            
            Logg.Log($"[ResourceLoader] finished loading label '{label}' assets", 
                Logg.LoggingMode.Completed);

            loadStatus[label] = LoadStatus.Done;
            NotifyPreLoadDone(label);// 리소스 로딩 대기중인 클래스들에게 로딩 완료 이벤트 전달
            
            Addressables.Release(locationHandles);
        }

        private static async UniTask LoadAndInitAsync<T>(AsyncOperationHandle<T> handle, Func<T, UniTask> onDoneAsync, CancellationToken token)
        {
            await handle.ToUniTask(cancellationToken: token);

            if (handle.Status != AsyncOperationStatus.Succeeded) return;
            if (onDoneAsync != null)
                await onDoneAsync(handle.Result);
        }

        private async UniTask DoAsyncInitialize(object obj, CancellationToken token)
        {
            if (obj is IAsyncInitializer asyncInitializer)
            {
                await asyncInitializer.InitializeAsync(token);
                Logg.Log($"[{GetType().Name}.DoAsyncInitialize] {obj.GetType().Name}", Logg.LoggingMode.Completed);
            }
        }

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (resourceKeys.TryGetValue(key, out AsyncOperationHandle cachedHandle))
            {
                return (T)cachedHandle.Result;
            }

            var op = Addressables.LoadAssetAsync<T>(key);
            resourceKeys[key] = op;

            var result = await op.ToUniTask(cancellationToken: token);
            if (result is IAsyncInitializer asyncInitializer)
                await asyncInitializer.InitializeAsync(token);
            
            return result;
        }

        // AssetReference로 단일 리소스를 비동기 로드
        // 이미 캐시에 있으면 즉시 반환
        // 없으면 AssetReference를 통해 로드 후 캐시
        public async UniTask<T> LoadAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (assetRef == null)
            {
                Logg.LogError($"[LoadAsync] reference is null.");
                return null;
            }

            if (!assetRef.RuntimeKeyIsValid())
            {
                Logg.LogError($"[LoadAsync] Invalid RuntimeKey for AssetReference<{typeof(T).Name}>. Asset: (name:{assetRef.Asset?.name}, guid: {assetRef.AssetGUID})");
                return null;
            }
            
            // case: AssetReference에 대응하는 핸들이 딕셔너리에 존재하고, 유효한 핸들인 경우
            if (resourceGuids.TryGetValue(assetRef.AssetGUID, out var cachedHandle) && cachedHandle.IsValid())
            { 
                return cachedHandle.Result as T; // 즉시 핸들과 연결된 리소스를 반환
            }

            // case: 캐싱된 핸들이 없는 경우
            var handle = assetRef.OperationHandle.IsValid() // 핸들을 추가로 생성 및 유효성 검사
                ? assetRef.OperationHandle
                : assetRef.LoadAssetAsync<T>();

            await handle.ToUniTask(cancellationToken: token); // 핸들로부터 리소스 로드
            // 리소스 로드에 실패했다면 null 반환
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Logg.LogError($"[LoadAsync] Load failed for AssetReference<{typeof(T).Name}> with key: {assetRef.RuntimeKey}");
                return null;
            }
            // AssetReference로부터 리소스 로드에 성공했다면 핸들을 캐싱 및 리소스 반환
            resourceGuids[assetRef.AssetGUID] = handle;
            return handle.Result as T;
        }

        #endregion

        #region TryLoad (Sync)

        // 로딩 완료된 리소스 목록에 접근 (key 기반)
        public bool TryLoad<T>(string key, out T resource) where T : UnityEngine.Object
        {
            Logg.Log($"[{GetType().Name}.TryLoad] (key: {key}, resource: {resourceKeys[key].Result})", Logg.LoggingMode.Completed);
            
            if (resourceKeys.TryGetValue(key, out var result)
                && result.Result is T cachedResource)
            {
                resource = cachedResource;
                return true;
            }
            
            resource = null;
            return false;
        }
        // 로딩 완료된 리소스 목록에 접근 (AssetReference 기반)
        public bool TryLoad<T>(AssetReference assetRef, out T resource) where T : UnityEngine.Object
        {
            if (resourceGuids.TryGetValue(assetRef.AssetGUID, out var result)
                && result.Result is T cachedResource)
            {
                resource = cachedResource;
                return true;
            }
            
            resource = null;
            return false;
        }

        #endregion
        
        // 특정 라벨 로드 상태 확인
        public bool IsLoadedAll(string label)
        {
            return loadStatus.TryGetValue(label, out var status) && (int)status == (int)LoadStatus.Done;
        }

        #region Progress

        // 라벨별 가중치
        private static readonly Dictionary<string, float> LabelWeights = new()
        {
            { nameof(PreLoadLabels.PreLoad_First), 0.10f },
            { nameof(PreLoadLabels.PreLoad_Asset), 0.10f },
            { nameof(PreLoadLabels.PreLoad_DataSO), 0.30f },
            { nameof(PreLoadLabels.PreLoad_CatalogSO), 0.30f },
            { nameof(PreLoadLabels.PreLoad_Prefab), 0.10f },
            { nameof(PreLoadLabels.PreLoad_Last), 0.20f },
        };
        
        // 전체 진행도 broadcaster 및 캐시
        private readonly MessageBroadcaster<(float, string)> _globalProgressMessage = new();
        private float _globalProgressCache;


        // 라벨별 progress broadcaster 저장소
        private readonly Dictionary<string, MessageBroadcaster<float>> _preloadProgress = new();
        private readonly Dictionary<string, float> _preloadProgressCache = new(); // 현재값 캐시(선택)

        // 라벨별 진행도 구독
        public IBroadcastSubscription SubscribePreLoadProgress(string label, Action<(float, string)> onProgress, bool fireCurrent = true)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentNullException(nameof(label));
            if (onProgress == null) throw new ArgumentNullException(nameof(onProgress));

            if (!_preloadProgress.TryGetValue(label, out var broadcaster))
            {
                broadcaster = new MessageBroadcaster<float>();
                _preloadProgress[label] = broadcaster;
                _preloadProgressCache[label] = 0f;
            }

            if (fireCurrent && _preloadProgressCache.TryGetValue(label, out var current))
                onProgress.Invoke((current, label));
            // 타겟 라벨을 캡처한 람다 형식으로 구독
            return broadcaster.Subscribe(value => onProgress.Invoke((value, label)));
        }

        private void ReportPreLoadProgress(string label, float p)
        {
            Logg.Log($"[{GetType().Name}] ReportPreLoadProgress label: {label}, progress: {p}", Logg.LoggingMode.Completed);
            p = Mathf.Clamp01(p);

            _preloadProgressCache[label] = p;

            if (_preloadProgress.TryGetValue(label, out var broadcaster))
                broadcaster.Report(p);
            else Logg.LogWarning($"[{GetType().Name}] ReportPreLoadProgress label: {label}, progress: {p} is ignored");
            
            // 전체 진행도 계산 및 보고
            ReportGlobalProgress(label);
        }
        
        private void InitLabelProcess(string label)
        {
            if (!_preloadProgress.ContainsKey(label))
                _preloadProgress[label] = new MessageBroadcaster<float>();
            _preloadProgressCache[label] = 0f;
            ReportPreLoadProgress(label, 0f);
        }

        // 전체 진행도 계산 및 보고
        private void ReportGlobalProgress(string label)
        {
            float global = 0f;
            foreach (var kvp in LabelWeights)
            {
                if (_preloadProgressCache.TryGetValue(kvp.Key, out var labelProgress))
                    global += labelProgress * kvp.Value;
            }
            
            _globalProgressCache = global;
            _globalProgressMessage.Report((global, label));
            
            Logg.Log($"[{GetType().Name}] GlobalProgress: {global:P1}", Logg.LoggingMode.Completed);
        }
        
        // 전체 PreLoad 진행도 구독
        public IBroadcastSubscription SubscribeGlobalPreLoadProgress(Action<(float, string)> onProgress, bool fireCurrent = true)
        {
            if (onProgress == null) throw new ArgumentNullException(nameof(onProgress));
            
            if (fireCurrent) // 구독 시점 진행도 반영 필요 시 임시로 빈 문자열로 반환
                onProgress.Invoke((_globalProgressCache, ""));
            
            return _globalProgressMessage.Subscribe(onProgress);
        }


        #endregion
    }
}


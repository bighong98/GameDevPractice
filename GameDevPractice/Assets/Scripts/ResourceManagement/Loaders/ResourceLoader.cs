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
        // 라벨별 에셋 번들 로드 상태 추적용 캐시
        private readonly Dictionary<string, LoadStatus> loadStatus = new Dictionary<string, LoadStatus>();
        // 라벨 단위로 일괄 리소스 로드 완료 알림 이벤트
        public event Action<string> OnLabelResourcesLoadedAll; 
        // 라벨별 프리로드 완료 대기 후속 작업 큐 목록 캐시
        // -> 라벨 완료 시점 순차 실행함
        private readonly Dictionary<string, Queue<Action>> reservedPreLoadTasks = new();
        
        #region Enums
        // 어드레서블 프리로드 라벨
        // 동일 이름 어드레서블 라벨이 존재해야함
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

        // 생성 직후 라벨 상태/진행도 초기화 진입점
        public ResourceLoader()
        {
            Init();  
        }

        #region Initialization

        private void Init()
        {
            // 선언된 프리로드 라벨 전체 초기 상태 등록
            foreach (var label in Enum.GetNames(typeof(PreLoadLabels)))
            {
                InitForLabel(label);
            }
        }
        
        private void InitForLabel(string label)
        {
            // 라벨 로드 상태 초기값 설정
            loadStatus[label] = LoadStatus.NotInitialized;
            // 라벨 완료 전 보류 콜백 큐 준비
            reservedPreLoadTasks[label] = new Queue<Action>();

            // 라벨 진행도 broadcaster/cached progress 초기화
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
                // 프리로드 라벨 순차 처리 정책
                Logg.Log($"[ResourceLoader] start to load label '{label}' assets", Logg.LoggingMode.Completed);
                await LoadAllAsync<UnityEngine.Object>(label);
            }
        }

        // 특정 어드레서블 라벨 로드까지 대기 콜백 등록
        // 이미 완료된 리소스 라벨인 경우 즉시 콜백 실행
        public void WaitForPreLoad(string label, Action callback)
        {
            // 이미 완료된 라벨 즉시 콜백 실행 정책
            if (IsLoadedAll(label))
            {
                callback?.Invoke();
                return;
            }
            
            // 등록되지 않은 라벨 방어 가드
            if (!reservedPreLoadTasks.TryGetValue(label, out var queue))
            {
                Logg.LogWarning($"[ResourceLoader] invalid preload label accepted");
                return;
            }
            
            // 라벨 완료 시점 실행용 큐 적재
            queue.Enqueue(callback);
        }

        private void NotifyPreLoadDone(string label)
        {
            // 내부 대기 작업 실행 + 외부 구독자에게 해당 라벨 리소스 로드 완료 알림
            RunReserved(label);
            OnLabelResourcesLoadedAll?.Invoke(label);
        }
        // 특정 어드레서블 라벨에 소속된 리소스 일괄 로드가 끝난 경우 대기 중인 작업들 실행
        private void RunReserved(string label)
        {
            // 대기중인 작업이 없을 경우 즉시 리턴
            if (!reservedPreLoadTasks.TryGetValue(label, out var queue) || queue.Count <= 0) return;
            while (queue.TryDequeue(out var task))
            {
                task?.Invoke();
            }
        }

        #endregion

        #region Load (Async)
        
        // 라벨에 매칭되는 모든 리소스 로케이션을 조회한 뒤, 각 항목을 비동기 로드
        // 개별 리소스 로드가 완료될 때마다 callback(진행도 확인 목적) 전달
        private async UniTask LoadAllAsync<T>(string label, Action<string, int, int> callback = null, CancellationToken token = default)
            where T : UnityEngine.Object
        {
            // 라벨에 연결된 로케이션 메타 데이터 조회
            var locationHandles = Addressables.LoadResourceLocationsAsync(label, typeof(T));
            var locations = await locationHandles.ToUniTask(cancellationToken: token);

            int totalCount = locations.Count;
            int loadCount = 0;

            try {
                // 내부 리소스가 없는 라벨은 즉시 100% 완료 처리
                if (totalCount <= 0) return;
                
                var tasks = new List<UniTask>(totalCount);
                foreach (var loc in locations)
                {
                    string key = loc.PrimaryKey;
                    // 동일 키 중복 로드 방지
                    if (resourceKeys.ContainsKey(key))
                    {
                        Interlocked.Increment(ref loadCount);
                        continue;
                    }

                    // 키 기반 핸들 캐시 선등록
                    var handle = Addressables.LoadAssetAsync<T>(loc);
                    resourceKeys[key] = handle;
                    
                    tasks.Add(LoadAndInitAsync(handle, 
                        // 필요한 경우 IAsyncInitializer 기반 추가 비동기 초기화 실행
                        onSucceedAsync: async (asset) => await DoAsyncInitialize(asset, token),
                        onComplete: () =>
                        {
                            // 완료 카운트 + 외부 진행도 콜백 보고
                            Interlocked.Increment(ref loadCount);
                            callback?.Invoke(key, loadCount, totalCount);
                            Logg.Log($"[ResourceLoader] finished loading: ({label} - {key}:{handle.Result})" +
                                $"({loadCount}/{totalCount}, {loadCount / (float)totalCount})", Logg.LoggingMode.Completed);
                            // 라벨 단위 진행도 캐시/브로드캐스트 반영
                            ReportPreLoadProgress(label, (totalCount <= 0) ? 1f : (loadCount / (float)totalCount));
                        },
                        token: token));
                }
                await UniTask.WhenAll(tasks);
            }
            catch (Exception e) { Logg.LogError($"Exception occured while loading assets in label: {label} - {e}"); }
            finally
            {
                Logg.Log($"[ResourceLoader] finished loading label '{label}' assets", 
                Logg.LoggingMode.Completed);

                // finally 경로에서 라벨 완료 상태로 강제 반영
                ReportPreLoadProgress(label, 1f);
                loadStatus[label] = LoadStatus.Done; //todo: 예외 발생 시 LoadStatus 다르게 설정할지 고려
                NotifyPreLoadDone(label);// 리소스 로딩 대기중인 클래스들에게 로딩 완료 이벤트 전달
                
                Addressables.Release(locationHandles);
            }
        }

        // 단일 핸들 로드 + 성공 시 초기화 훅 + 완료 콜백 통합
        private static async UniTask LoadAndInitAsync<T>(
            AsyncOperationHandle<T> handle, 
            Func<T, UniTask> onSucceedAsync, 
            Action onComplete,
            CancellationToken token)
        {
            try
            {
                // 취소 토큰 연동 핸들 완료 대기
                await handle.ToUniTask(cancellationToken: token);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    // 성공 후처리 훅 존재 시에만 실행
                    if (onSucceedAsync == null) return;
                    await onSucceedAsync(handle.Result);
                }
                else Logg.LogError($"{nameof(LoadAndInitAsync)} - Asset load failed: {handle.DebugName}");
            }
            catch (Exception e) { 
                Logg.LogError($"Exception occured while {nameof(LoadAndInitAsync)} loading asset: {handle.DebugName} - {e}");}
            finally { onComplete?.Invoke(); }
        }

        // IAsyncInitializer 구현 객체에 대한 비동기 초기화 진입점
        private async UniTask DoAsyncInitialize(object obj, CancellationToken token)
        {
            // 초기화 인터페이스 미구현 객체 조기 반환
            if (obj is not IAsyncInitializer asyncInitializer) return;

            await asyncInitializer.InitializeAsync(token);
            Logg.Log($"[{GetType().Name}.DoAsyncInitialize] {obj.GetType().Name}", Logg.LoggingMode.Completed);
        }

        // key 기반 단일 리소스 비동기 로드 API
        // -> 캐시 우선 반환, 미존재 시 주소기반 로드 수행
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
                Logg.LogError($"[LoadAsync] Invalid RuntimeKey for AssetReference<{typeof(T).Name}>. \n" +
                                "Asset: (name:{(assetRef.Asset.IsNotNull() ? assetRef.Asset.name : string.Empty)}, " +
                                "guid: {assetRef.AssetGUID})");
                return null;
            }

            if (resourceGuids.TryGetValue(assetRef.AssetGUID, out var cachedHandle) && cachedHandle.IsValid())
            {
                var cachedResult = cachedHandle.Result as T;
                if (cachedResult is IAsyncInitializer cachedInitializer)
                {
                    await cachedInitializer.InitializeAsync(token);
                }

                return cachedResult;
            }

            var handle = assetRef.OperationHandle.IsValid()
                ? assetRef.OperationHandle
                : assetRef.LoadAssetAsync<T>();

            await handle.ToUniTask(cancellationToken: token);
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Logg.LogError($"[LoadAsync] Load failed for AssetReference<{typeof(T).Name}> with key: {assetRef.RuntimeKey}");
                return null;
            }

            resourceGuids[assetRef.AssetGUID] = handle;

            var loadedResult = handle.Result as T;
            if (loadedResult is IAsyncInitializer loadedInitializer)
            {
                await loadedInitializer.InitializeAsync(token);
            }

            return loadedResult;
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
            // Done 상태 정수 비교 기반 완료 판정
            return loadStatus.TryGetValue(label, out var status) && (int)status == (int)LoadStatus.Done;
        }

        #region Progress

        // 라벨별 진행도 합산 가중치 테이블
        private static readonly Dictionary<string, float> LabelWeights = new()
        {
            { nameof(PreLoadLabels.PreLoad_First), 0.10f },
            { nameof(PreLoadLabels.PreLoad_Asset), 0.10f },
            { nameof(PreLoadLabels.PreLoad_DataSO), 0.30f },
            { nameof(PreLoadLabels.PreLoad_CatalogSO), 0.30f },
            { nameof(PreLoadLabels.PreLoad_Prefab), 0.10f },
            { nameof(PreLoadLabels.PreLoad_Last), 0.20f },
        };
        
        // 전체 진행도 브로드캐스터 및 최신 캐시
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

            // 라벨 broadcaster 지연 생성
            if (!_preloadProgress.TryGetValue(label, out var broadcaster))
            {
                broadcaster = new MessageBroadcaster<float>();
                _preloadProgress[label] = broadcaster;
                _preloadProgressCache[label] = 0f;
            }

            // 구독 직후 현재값 1회 전달 옵션
            if (fireCurrent && _preloadProgressCache.TryGetValue(label, out var current))
                onProgress.Invoke((current, label));
            // 타겟 라벨을 캡처한 람다 형식으로 구독
            return broadcaster.Subscribe(value => onProgress.Invoke((value, label)));
        }

        private void ReportPreLoadProgress(string label, float p)
        {
            Logg.Log($"[{GetType().Name}] ReportPreLoadProgress label: {label}, progress: {p}", Logg.LoggingMode.Completed);
            // 0~1 범위 정규화
            p = Mathf.Clamp01(p);

            // 최신 라벨 진행도 캐시 갱신
            _preloadProgressCache[label] = p;

            // 라벨 브로드캐스터 존재 시점에만 이벤트 전파
            if (_preloadProgress.TryGetValue(label, out var broadcaster))
                broadcaster.Report(p);
            else Logg.LogWarning($"[{GetType().Name}] ReportPreLoadProgress label: {label}, progress: {p} is ignored");
            
            // 전체 진행도 계산 및 보고
            ReportGlobalProgress(label);
        }
        
        private void InitLabelProcess(string label)
        {
            // 라벨 진행도 채널 미생성 시 초기 생성
            if (!_preloadProgress.ContainsKey(label))
                _preloadProgress[label] = new MessageBroadcaster<float>();
            // 라벨 진행도 초기값 고정 및 첫 보고
            _preloadProgressCache[label] = 0f;
            ReportPreLoadProgress(label, 0f);
        }

        // 전체 진행도 계산 및 보고
        private void ReportGlobalProgress(string label)
        {
            float global = 0f;
            foreach (var kvp in LabelWeights)
            {
                // 라벨별 진행도 * 가중치 누적 합산
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
            
            // 구독 시점 최신 전역 진행도 즉시 전달 옵션
            if (fireCurrent) // 구독 시점 진행도 반영 필요 시 임시로 빈 문자열로 반환
                onProgress.Invoke((_globalProgressCache, ""));
            
            return _globalProgressMessage.Subscribe(onProgress);
        }


        #endregion

        #region 

        public void Unload(string key)
        {
            //todo: 어드레서블 키 기반 리소스 조회 후 리소스 언로드 + 핸들 정리
        }

        public void Unload(AssetReference key)
        {
            //todo: AssetReference 기반 리소스 조회 후 리소스 언로드 + 핸들 정리
        }

        #endregion
    }
}


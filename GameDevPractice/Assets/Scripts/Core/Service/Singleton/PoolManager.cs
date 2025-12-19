using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.Utils;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Scripting;

namespace TH.Core.Service
{
    [Preserve]
    public class PoolManager : Singleton<PoolManager>, ISingleton
    {
        // <prefab-오브젝트 풀> 목록
        private readonly Dictionary<GameObject, ObjectPool<IPoolObject>> pools = new Dictionary<GameObject, ObjectPool<IPoolObject>>();
        private readonly PoolContainer poolContainer = new PoolContainer(); // 풀 오브젝트 컨테이너 생성 담당, serialize for debug

        private const int DefaultCapacity = 10; // 풀 초기 생성 개수(생성 직후 실제 생성되진 않고 필요 시 lazy하게 생성됨)
        private const int DefaultMaxSize = 100; // 풀 상한 (초과 시 Release 대신 Destroy 로직 수행)
        private const string PoolRootName = "Pool_Root";
        
        private PoolManager()
        {
            SetPoolContainerRoot();
        }

        #region Initialization

        private void SetPoolContainerRoot()
        {
            var root = new GameObject(PoolRootName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            poolContainer.Init(root.transform);
        }

        #endregion

        #region ISingleton 
        public UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            if (externalToken.IsCancellationRequested) return UniTask.CompletedTask;
            
            poolContainer.ReleaseAllPooledObjects();
            return UniTask.CompletedTask;
        }

        public UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            return UniTask.CompletedTask;
        }
        #endregion

        #region Get/Create Pool

        // prefab에 대응하는 ObjectPool을 반환(없으면 생성 -> CreatePool)
        // registerPool: false -> 풀 딕셔너리에 등록하지 않고 생성된 풀 반납 (풀 요청 측에서 직접 관리)
        // parent: null -> PoolContainer로 컨테이너 자동 생성
        // create/get/release: 풀 이벤트에 콜백 등록 가능
        public ObjectPool<IPoolObject> GetPool(GameObject prefab, Transform parent = null,
            Action<IPoolObject> createAction = null, Action<IPoolObject> getAction = null, Action<IPoolObject> releaseAction = null,
            int capacity = DefaultCapacity, int maxSize = DefaultMaxSize, bool registerPool = true)
        {
            if (prefab == null)
            {
                Logg.LogError($"[{nameof(PoolManager)}.{nameof(GetPool)}] Prefab is null");
                return null;
            }
            
            if (prefab.GetComponent<IPoolObject>() is not { } instance)
            {
                Logg.LogError($"[{nameof(PoolManager)}.{nameof(GetPool)}] Prefab '{prefab.name}' does not have IPoolObject component");
                return null;
            }
            
            capacity = (capacity == 0) ? DefaultCapacity : capacity;
            maxSize = (maxSize == 0) ? DefaultMaxSize : maxSize;
            
            if (parent == null)
                parent = poolContainer.GetPoolContainer(prefab, CacheAndGetType(prefab));
            
            if (registerPool == false)
                return CreatePool(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize);

            if (!pools.TryGetValue(prefab, out var pool))
            {
                if (CreatePool(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize)
                    is not { } newPool) return null;
                pool = newPool;
                pools[prefab] = newPool;
            }

            return pool;
        }

        // 오브젝트 풀 생성
        // create/get/release/destroy 추가 콜백을 Unity ObjectPool에 연결 (각각 동명의 IPoolObject 수명 이벤트 타이밍에 호출)
        // Origin 참조를 IPoolObject에 기록하여 동일성 관리 (Release 시 Origin을 기준으로 소속 풀을 탐색함. Origin은 반드시 prefab 이어야함)
        private ObjectPool<IPoolObject> CreatePool(GameObject prefab, Transform parent,
            Action<IPoolObject> createAction, Action<IPoolObject> getAction, Action<IPoolObject> releaseAction,
            int capacity, int maxSize)
        {
            var pool = new ObjectPool<IPoolObject>(
                createFunc: () =>
                {
                    if (UnityEngine.Object.Instantiate(prefab) is not { } instance
                        || instance.GetComponent<IPoolObject>() is not {} poolObj) return null;

                    if (poolObj is Component poolObjComp)
                        poolObjComp.transform.SetParent(parent, worldPositionStays: false);
                    
                    poolObj.Origin = prefab;
                    
                    createAction?.Invoke(poolObj);
                    poolObj.OnCreateFromPool();
                    
                    return poolObj;
                },
                actionOnGet: obj =>
                {
                    if (obj is Component c)
                        c.gameObject.SetActive(true);

                    getAction?.Invoke(obj);
                    obj.OnGetFromPool();
                },
                actionOnRelease: obj =>
                {
                    if (obj is Component { gameObject: { activeSelf: true } } c )
                    {
                        releaseAction?.Invoke(obj);
                        obj.OnReleaseFromPool();
                        c.gameObject.SetActive(false);
                    }
                },
                actionOnDestroy: obj =>
                {
                    obj.OnDestroyFromPool();
                    if (obj is Component c)
                        UnityEngine.Object.Destroy(c.gameObject);
                },
                defaultCapacity: capacity,
                maxSize: maxSize
            );

            return pool;
        }
        
        #endregion

        #region Get Object From Pool
        // 비제네릭 버전
        public IPoolObject GetFromPool(GameObject prefab)
        {
            if (GetPool(prefab) is { } pool) return pool.Get();
            return null;
        }
        // 제네릭 버전 
        // parent: not null -> 꺼낸 뒤 임의로 상위 오브젝트(parent) 지정
        public T GetFromPool<T>(GameObject prefab, Transform parent = null, bool worldPositionStays = true) where T : Component, IPoolObject
        {
            if (GetPool(prefab, parent) is not { } pool || pool.Get() is not T t) return null;
            
            if (parent != null)
                t.transform.SetParent(parent, worldPositionStays: worldPositionStays); // parent로 상위 오브젝트 변경 및 기존 worldPosition 유지

            return t;
        }
        // GetFromPool<T> + 위치 지정
        public T GetFromPool<T>(GameObject prefab, Transform parent, Vector3 position) where T : Component, IPoolObject
        {
            if (GetPool(prefab, parent) is not { } pool || pool.Get() is not T t) return null;
            
            if (parent != null)
                t.transform.SetParent(parent, worldPositionStays: false);

            if (t.transform is {} trs)
                trs.position = position;

            return t;
        }
        // GetFromPool<T> + 위치 + 회전 지정
        public T GetFromPool<T>(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component, IPoolObject
        {
            if (GetPool(prefab, parent) is not { } pool || pool.Get() is not T t) return null;
            
            if (parent != null)
                t.transform.SetParent(parent, worldPositionStays: false);

            if (t.transform is {} trs)
            {
                trs.position = position;
                trs.rotation = rotation;
            }
            
            return t;
        }

        #endregion

        #region Release
        // 오브젝트를 풀에 반납
        // IPoolObject.Origin을 기준으로 소속 풀을 탐색
        // Origin은 반드시 prefab 게임 오브젝트여야 함
        public void ReleaseFromPool(IPoolObject obj)
        {
            if (obj == null)
            {
                Logg.LogError($"[{nameof(PoolManager)}.{nameof(ReleaseFromPool)}] Object is null");
                return;
            }
            
            if (obj.Origin == null)
            {
                Logg.LogError($"[{nameof(PoolManager)}.{nameof(ReleaseFromPool)}] Object.Origin is null. Object: {(obj as UnityEngine.Object)?.name ?? "Unknown"}");
                return;
            }

            if (pools.TryGetValue(obj.Origin, out var pool))
            {
                pool.Release(obj);
            }
            else
            {
                Logg.LogWarning($"[{nameof(PoolManager)}.{nameof(ReleaseFromPool)}] Pool not found for origin: {obj.Origin.name}");
            }
        }

        #endregion

        #region Helper Methods
        // (prefab -> IPoolObject) 구현 타입 캐시 (PoolContainer 그룹핑용)
        private readonly Dictionary<GameObject, Type> cachedTypeForPrefab = new Dictionary<GameObject, Type>();
        
        private Type CacheAndGetType(GameObject prefab)
        {
            if (!cachedTypeForPrefab.TryGetValue(prefab, out var type))
            {
                type = prefab.GetComponent<IPoolObject>().GetType();
                cachedTypeForPrefab[prefab] = type;
            }

            return type;
        }

        #endregion

        #region Debug

        public void LogAllPoolStatus()
        {
            foreach (var pool in pools)
            {
                Logg.Log($"[{nameof(PoolManager)}] prefab: {pool.Key}, pool: {pool.Value}");
            }
        }

        #endregion

    }
}


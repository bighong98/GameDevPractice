using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TH.Core.Pool
{
    public class PoolManager : Singleton<PoolManager>
    {
        private const int DefaultCapacity = 10;
        private const int DefaultMaxSize = 100;
        private readonly Dictionary<GameObject, ObjectPool<IPoolObject>> pools = new Dictionary<GameObject, ObjectPool<IPoolObject>>();
        [SerializeField] private PoolContainer poolContainer = new PoolContainer();
        
        #region Initialization

        protected override void InitOnce()
        {
            // GameSceneManager.Instance.RegisterCleanupTask(async () =>
            // {
            //     await Clear();  
            // });
            //
            poolContainer?.Init(transform);
        }

        protected override void InitOnceAfterPreLoad(bool isLoadCompleted)
        {
        
        }

        protected override void Init()
        {
            // ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
            // GameSceneManager.Instance.RegisterCleanupTask(async () =>
            // {
            //     await Clear();  
            // });
        }

        protected override void InitAfterPreLoad(bool isLoadCompleted)
        {
        
        }

        #endregion

        #region Get/Create Pool

        public ObjectPool<IPoolObject> GetPool(GameObject prefab, Transform parent = null,
            Action<IPoolObject> createAction = null, Action<IPoolObject> getAction = null, Action<IPoolObject> releaseAction = null,
            int capacity = DefaultCapacity, int maxSize = DefaultMaxSize, bool registerPool = true)
        {
            if (prefab.GetComponent<IPoolObject>() is not { } instance) return null;
            
            capacity = (capacity == 0) ? DefaultCapacity : capacity; // capacity가 0이면 대신 디폴트 값 적용
            maxSize = (maxSize == 0) ? DefaultMaxSize : maxSize; // maxSize가 0이면 대신 디폴트 값 적용
            
            if (parent == null)
                parent = poolContainer.GetPoolContainer(prefab, CacheAndGetType(prefab));
            
            if (registerPool == false)
                return CreatePool(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize);

            if (!pools.TryGetValue(prefab, out var pool)) // 기존 오브젝트 풀이 존재하는지 확인
            {
                if (CreatePool(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize)
                    is not { } newPool) return null; // 신규 풀 생성 시도, 실패 시 null 반환
                pool = newPool; 
                pools[prefab] = newPool; // 풀 생성 성공 시 풀 목록에 반영 
            }

            return pool;
        }


        private ObjectPool<IPoolObject> CreatePool(GameObject prefab, Transform parent,
            Action<IPoolObject> createAction, Action<IPoolObject> getAction, Action<IPoolObject> releaseAction,
            int capacity, int maxSize)
        {
            var pool = new ObjectPool<IPoolObject>(
                createFunc: () =>
                {
                    if (Instantiate(prefab) is not { } instance
                        || instance.GetComponent<IPoolObject>() is not {} poolObj) return null;

                    if (poolObj is Component poolObjComp)
                    {
                        poolObjComp.transform.SetParent(parent, worldPositionStays: false);
                    }
                    
                    poolObj.Origin = prefab;
                    poolObj.PoolKey = new PoolKey(prefab, poolObj.GetType()); // todo: GetType() 반복호출 최적화
                    
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
                        Destroy(c.gameObject);
                },
                defaultCapacity: capacity,
                maxSize: maxSize
            );

            return pool;
        }
        
        #endregion

        #region Get Object From Pool

        public IPoolObject GetFromPool(GameObject prefab)
        {
            if (GetPool(prefab) is { } pool) return pool.Get();
            return null;
        }

        public T GetFromPool<T>(GameObject prefab, Transform parent = null) where T : Component, IPoolObject
        {
            if (GetPool(prefab, parent) is not { } pool || pool.Get() is not T t) return null;
            
            if (parent != null)
                t.transform.SetParent(parent, worldPositionStays: true);

            return t;
        }
        
        public T GetFromPool<T>(GameObject prefab, Transform parent, Vector3 position) where T : Component, IPoolObject
        {
            if (GetPool(prefab, parent) is not { } pool || pool.Get() is not T t) return null;
            
            if (parent != null)
                t.transform.SetParent(parent, worldPositionStays: false);

            if (t.transform is {} trs)
                trs.position = position;

            return t;
        }
        
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

        public void ReleaseFromPool(IPoolObject obj)
        {
            if (obj == null)
            {
                Util.Log($"[{nameof(PoolingManager)}.{nameof(ReleaseFromPool)}()] is null");
                return;
            }

            if (obj is not Component {} compo || obj.Origin == null)
            {
                Util.Log($"[{nameof(PoolingManager)}.{nameof(ReleaseFromPool)}()] {obj}: component or Origin is null");
                return;
            }

            // if (obj.PoolKey is not { Prefab: { } prefab, KeyType: { } keyType })
            // {
            //     Util.Log($"[{nameof(PoolingManager)}.{nameof(ReleaseFromPool)}()] {obj}: PoolKey is not valid ");
            //     return;
            // }

            if (pools.TryGetValue(obj.Origin, out var pool))
            {
                pool.Release(obj);
            }
        }

        #endregion

        #region Helper Methods

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
                Util.Log($"[{nameof(PoolManager)}] prefab: {pool.Key}, pool: {pool.Value}");
            }
        }

        #endregion

        protected override UniTask Clear()
        {
            base.Clear();
            bool onlyRelease = true;
            if (onlyRelease)
            {
                poolContainer?.ReleaseAllPooledObjects();
            }

            return UniTask.CompletedTask;
        }
    }
}


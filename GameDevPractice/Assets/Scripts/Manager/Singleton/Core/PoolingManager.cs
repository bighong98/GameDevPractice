using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using RPG.UI;

public abstract class PoolDictWrapperBase { // 제네릭 사용 목적 래퍼의 래퍼
    public abstract void Clear();
} 
public class PoolDictWrapper<T> : PoolDictWrapperBase where T : UnityEngine.Component, IPoolObject // 제네릭 사용 목적 딕셔너리 래퍼
{
    public readonly Dictionary<GameObject, ObjectPool<T>> Pools = new Dictionary<GameObject, ObjectPool<T>>();
    public override void Clear()
    {
        foreach (var pool in Pools.Values)
        {
            pool.Clear();
        }
    }
}
public class PoolingManager : Singleton<PoolingManager>
{
    private const int DefaultCapacity = 10;
    private const int DefaultMaxSize = 100;
    private readonly Dictionary<Type, PoolDictWrapperBase> typePoolDictionary = new Dictionary<Type, PoolDictWrapperBase>();
    [SerializeField] private PoolContainer poolContainer = new PoolContainer();
    protected override void Awake()
    {
        base.Awake();
        if (IsInvalidInstance()) return; // 중복 인스턴스인 경우 Init() 실행x
        
        Init();
        poolContainer?.Init(transform);
    }

    protected override void OnSceneLoaded(bool dummy)
    {
        Init();
    }
    
    private void Init()
    {
        ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
        GameSceneManager.Instance.RegisterCleanupTask(async () =>
        {
            await Clear();  
        });
    }

    private void InitAfterLoad(bool dummy)
    {
        
    }

    #region Get
    // 외부에서 오브젝트 풀을 생성할 때 사용 (이미 오브젝트 풀이 존재하면 기존 풀 반환, 없으면 새로 생성)
    // prefab 인자에 반드시 프리팹 사용할 것 (일반 GameObject 사용 권장x)
    // getAction: GetFromPool<T>() 직후 호출, releaseAction: ReleaseFromPool<T>() 직후 호출
    // registerPool: PoolingManager의 딕셔너리에 오브젝트 풀을 등록할 건지 여부 (기본값: true)
    //// UI는 registerPool = false로 두고 UIManager의 popupPools에 저장해 사용할 것
    public ObjectPool<T> GetPool<T>(GameObject prefab, Transform parent = null, Action<T> createAction = null, Action<T> getAction = null, Action<T> releaseAction = null, int capacity = DefaultCapacity, int maxSize = DefaultMaxSize, bool registerPool = true) where T : UnityEngine.Component, IPoolObject
    { // 풀 탐색, 생성을 겸하는 public 함수. 반드시 인자로 프리펩을 사용할 것
        var instance = prefab.GetComponent<T>();
        if (instance == null) return null;
        
        capacity = (capacity == 0) ? DefaultCapacity : capacity; // capacity가 0이면 대신 디폴트 값 적용
        maxSize = (maxSize == 0) ? DefaultMaxSize : maxSize; // maxSize가 0이면 대신 디폴트 값 적용
        
        if (parent == null)
            parent = poolContainer.GetPoolContainer<T>(prefab);

        if (registerPool == false)
            return CreatePool<T>(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize);
        
        if (typePoolDictionary.TryGetValue(typeof(T), out var poolListWrapperBase))
        { // case: 같은 타입 클래스의 풀이 존재하는 경우
            if (poolListWrapperBase is PoolDictWrapper<T> poolListWrapper)
            {
                if (poolListWrapper.Pools.TryGetValue(prefab, out var pool))
                {
                    return pool; // 기존 풀이 있다면 반환
                }
                var newPool = CreatePool<T>(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize); // 존재하는 풀이 없다면 생성
                poolListWrapper.Pools[prefab] = newPool;
                return newPool;
            }
        }
        // case: 같은 타입 클래스의 풀이 존재하지 않는 경우
        var poolDict = typePoolDictionary[typeof(T)] = CreatePoolDictionary<T>();
        var poolInDict = ((PoolDictWrapper<T>)poolDict).Pools[prefab] = CreatePool<T>(prefab, parent, createAction, getAction, releaseAction, capacity, maxSize);
        
        return poolInDict;
    }
    
    private ObjectPool<T> CreatePool<T>(GameObject prefab, Transform parent, Action<T> createAction, Action<T> getAction, Action<T> releaseAction, int capacity, int maxSize) where T : UnityEngine.Component, IPoolObject
    { // 풀 생성 함수. GetPool을 통해 호출됨
        var pool = new ObjectPool<T>(
            createFunc: () =>
            {
                var component = Instantiate(prefab).GetComponent<T>();
                if (component != null)
                {
                    // todo: 레이어 설정 추가
                    // todo: worldPositionStays 값을 어떻게 결정할지 (CreatePool()의 인자로 받을지, 타입으로 자동 결정할지 등) 기획 후 수정
                    component.transform.SetParent(parent, worldPositionStays: false);
                    component.Origin = prefab;
                    createAction?.Invoke(component);
                    component.OnCreateFromPool();
                }
                return component;
            },
            actionOnGet: obj =>
            {
                obj.gameObject.SetActive(true);
                getAction?.Invoke(obj);
                obj.OnGetFromPool();
            },
            actionOnRelease: obj =>
            {
                if (obj.gameObject.activeSelf)
                {
                    releaseAction?.Invoke(obj);
                    obj.OnReleaseFromPool();
                    obj.gameObject.SetActive(false);
                }
            },
            actionOnDestroy: obj => obj.OnDestroyFromPool(),
            defaultCapacity: capacity,
            maxSize: maxSize
        );

        return pool;
    }

    public T GetFromPool<T>(GameObject prefab, Transform parent = null) where T : Component, IPoolObject
    {
        ObjectPool<T> pool = GetPool<T>(prefab, parent);
        return pool.Get();
    }

    public T GetFromPool<T>(GameObject prefab, Transform parent, Vector3 position) where T : Component, IPoolObject
    {
        ObjectPool<T> pool = GetPool<T>(prefab, parent);
        var clone = pool.Get();
        clone.transform.position = position; // 현재 OnGetFromPool()보다 늦게 실행됨
        return clone;
    }
    
    public T GetFromPool<T>(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component, IPoolObject
    {
        ObjectPool<T> pool = GetPool<T>(prefab, parent);
        var clone = pool.Get();
        if (clone.transform is {} trs)
        {
            trs.position = position;
            trs.rotation = rotation;
        }

        return clone;
    }
    
    #endregion
    
    #region Release

    public void ReleaseFromPool<T>(T component) where T : UnityEngine.Component, IPoolObject
    { // 풀링으로 생성된 오브젝트 반환 함수. 소속된 풀을 알 수 없을 때 사용
        if (component == null)
        {
            Util.Log($"{nameof(PoolingManager)}: component is null. failed to ReleaseFromPool");
            return;
        }

        if (component.Origin == null)
        {
            Util.Log($"{nameof(PoolingManager)}: {component.gameObject.name}.component.Origin is null. Destroying manually.");
            GameObject.Destroy(component.gameObject);
            return;
        }
        
        if (typePoolDictionary.TryGetValue(typeof(T), out var poolDict))
        {
            if(((PoolDictWrapper<T>)poolDict).Pools.TryGetValue(component.Origin, out var pool))
            {
                pool.Release(component);
                return;
            }
        }
    }

    public void ReleaseFromPool<T>(ObjectPool<T> pool, T component, bool poolCheck = true) where T : Component, IPoolObject
    { // 반환해야하는 풀을 알고 있는 경우 사용. 명확하게 출처 풀을 알고 있는 경우 poolCheck = false 해서 사용
        if (pool == null || component == null || component.Origin == null)
        {
            Util.Log($"{nameof(PoolingManager)}: component or Origin is null. Destroying manually.");
            GameObject.Destroy(component.gameObject);
            return;
        }
        
        if (poolCheck)
        {
            if (typePoolDictionary.TryGetValue(typeof(T), out var poolDict) &&
                ((PoolDictWrapper<T>)poolDict).Pools.ContainsValue(pool))
            {
                SucceedRelease();
                return;
            }
            
            FailRelease();
            return;
        }
        
        SucceedRelease();
        return;

        void SucceedRelease()
        {
            pool.Release(component);
        }

        void FailRelease()
        {
            Destroy(component.gameObject);
            Util.Log($"{nameof(PoolingManager)}: component doesn't match with pool. pool: {pool}, component: {component.gameObject.name}");
        }
    }

    #endregion

    #region For Debug

    public void LogAllPoolsStatus()
    { // PoolingManager에서 관리중인 모든 풀 정보 확인용 함수
        foreach (var (type, wrapper) in typePoolDictionary)
        {
            var poolDictField = wrapper.GetType().GetField("Pools");

            if (poolDictField?.GetValue(wrapper) is not IDictionary pools) continue;

            foreach (System.Collections.DictionaryEntry entry in pools)
            {
                var prefab = (GameObject)entry.Key;
                var pool = entry.Value;
                Util.Log($"[Pool] Type: {type}, Prefab: {prefab.name}, Pool: {pool}");
            }
        }
    }

    #endregion 

    #region Helper Method

    private PoolDictWrapper<T> CreatePoolDictionary<T>() where T : UnityEngine.Component, IPoolObject
    {
        return new PoolDictWrapper<T>();
    }

    #endregion
    
    private UniTask Clear(bool onlyRelease = true)
    {
        if (onlyRelease)
        {
            poolContainer?.ReleaseAllPooledObjects();
        }
        else
        {
            foreach (var poolWrapper in typePoolDictionary.Values)
            {
                poolWrapper.Clear();
            }

            typePoolDictionary.Clear();
            poolContainer?.Clear();
        }
        
        return UniTask.CompletedTask;
    }
}
[Serializable]
public class PoolContainer
{
    [SerializeField] private Transform TopParent;
    private readonly Dictionary<Type, Transform> TypeContainerDictionary = new Dictionary<Type, Transform>();
    private readonly Dictionary<GameObject, Transform> PoolContainerDictionary = new Dictionary<GameObject, Transform>();

    public void Init(Transform parent)
    {
        TopParent = parent;
    }
    
    public Transform GetPoolContainer<T>(GameObject prefab) where T : Component, IPoolObject
    {
        if (PoolContainerDictionary.TryGetValue(prefab, out var existingPoolContainer))
        {
            return existingPoolContainer;
        }

        GameObject newPoolContainer = new GameObject($"{prefab.name}");
        newPoolContainer.transform.SetParent(GetTypePoolContainer<T>());
        return PoolContainerDictionary[prefab] = newPoolContainer.transform;
    }
    private Transform GetTypePoolContainer<T>() where T : Component, IPoolObject
    {
        if (TypeContainerDictionary.TryGetValue(typeof(T), out var existingTypeContainer))
        {
            return existingTypeContainer;
        }
        
        GameObject newTypeContainer = new GameObject($"{typeof(T).Name}s");
        newTypeContainer.transform.SetParent(TopParent);
        return TypeContainerDictionary[typeof(T)] = newTypeContainer.transform;
    }

    public void ReleaseAllPooledObjects()
    {
        foreach (var poolContainer in PoolContainerDictionary.Values)
        {
            if (poolContainer.childCount == 0) continue; // 풀 컨테이너에 오브젝트 풀이 없으면 스킵
            
            for (int i = 0; i < poolContainer.childCount; i++)
            {
                var child = poolContainer.GetChild(i);
                if (child.gameObject.activeSelf && child.GetComponent<IPoolObject>() is { Origin: not null } pooledObject)
                {
                    pooledObject.ReleaseSelf();
                }
            }
        }
    }

    public void Clear()
    {
        TypeContainerDictionary.Clear();
        PoolContainerDictionary.Clear();
    }
}

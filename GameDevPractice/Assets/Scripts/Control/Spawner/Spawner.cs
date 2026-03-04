using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Collections.Generic;
using TH.Core.Pool;
using TH.Utils;
using TH.Core.Service;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.SaveLoad;

public class Spawner<T> : MonoBehaviour where T : UnityEngine.Component, IPoolObject
{
    [SerializeField] protected AssetReferenceGameObject prefabReference;
    [SerializeField] private bool resetToDefaultOnGetFromPool = false;
    
    [NonSerialized] protected GameObject prefab;

    protected ObjectPool<IPoolObject> pool;
    protected Action<IPoolObject> onCreate;
    protected Action<IPoolObject> onGet;
    protected Action<IPoolObject> onRelease;
    protected int ActiveObjectCount => activeObjects.Count;

    private bool isInit;
    private int configuredCapacity;
    private int configuredMax;
    private readonly HashSet<IPoolObject> activeObjects = new();
    private readonly List<IPoolObject> activeObjectBuffer = new();

    public bool HasPool => isInit && pool != null;
    public ObjectPool<IPoolObject> Pool => pool;
    public AssetReferenceGameObject PrefabReference => prefabReference;
    public GameObject Prefab => prefab;
    public bool ResetToDefaultOnGetFromPool { get => resetToDefaultOnGetFromPool; set => resetToDefaultOnGetFromPool = value; }

    protected virtual async void Start()
    {
        try
        {
            if (!await EnsurePrefabResolvedAsync(destroyCancellationToken))
                return;

            SetPool(prefab, onCreate, onGet, onRelease, configuredCapacity, configuredMax);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Logg.LogError($"[{gameObject.name}.{nameof(Spawner<T>)}.{nameof(Start)}] failed to resolve prefab. {e.Message}");
        }
    }

    protected async UniTask<bool> EnsurePrefabResolvedAsync(CancellationToken token = default)
    {
        if (prefabReference != null && prefabReference.RuntimeKeyIsValid())
        {
            if (ResourceManager.Instance == null)
            {
                Logg.LogError($"[{gameObject.name}.{nameof(Spawner<T>)}.{nameof(EnsurePrefabResolvedAsync)}] ResourceManager is null");
                return prefab != null;
            }

            var loadedPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(prefabReference, token);
            if (loadedPrefab != null)
            {
                prefab = loadedPrefab;
                return true;
            }

            Logg.LogError($"[{gameObject.name}.{nameof(Spawner<T>)}.{nameof(EnsurePrefabResolvedAsync)}] failed to load prefab from AssetReference");
        }

        return prefab != null;
    }

    
    public virtual void SetPool(GameObject prefab, Action<IPoolObject> createAction = null, Action<IPoolObject> getAction = null, Action<IPoolObject> releaseAction = null, int capacity = 0, int max = 0)
    {
        if (prefab == null)
        {
            Logg.LogError($"[{gameObject.name}.{nameof(Spawner<T>)}.{nameof(SetPool)}] prefab is null");
            return;
        }

        SetCallbacks(createAction, getAction, releaseAction);

        var requiresRebuild = RequiresPoolRebuild(prefab, capacity, max);
        this.prefab = prefab;
        configuredCapacity = capacity;
        configuredMax = max;

        if (!requiresRebuild)
            return;

        InitializePool(capacity, max);
    }

    protected void SetCallbacks(Action<IPoolObject> createAction = null, Action<IPoolObject> getAction = null, Action<IPoolObject> releaseAction = null)
    {
        if (createAction != null)
            onCreate = createAction;
        if (getAction != null)
            onGet = getAction;
        if (releaseAction != null)
            onRelease = releaseAction;
    }

    private bool RequiresPoolRebuild(GameObject targetPrefab, int capacity, int max)
    {
        if (!HasPool)
            return true;

        if (prefab != targetPrefab)
            return true;

        if (configuredCapacity != capacity)
            return true;

        return configuredMax != max;
    }

    protected virtual void InitializePool(int capacity, int max)
    {
        if (PoolManager.Instance.GetPool(prefab, null, HandleCreate, HandleGet, HandleRelease, capacity, max, registerPool: false)
                is { } newPool)
        {
            pool = newPool;
            isInit = true;
            return;
        }

        pool = null;
        isInit = false;
    }

    public T Spawn()
    {
        if (!HasPool)
        {
            Logg.Log($"{gameObject.name}.{nameof(Spawner<T>)}.Spawn: pool is null");
            return null;
        }

        if (pool.Get() is not T pooledObject)
            return null;

        return pooledObject;
    }

    public T Spawn(Vector3 pos)
    {
        var clone = Spawn();
        if (clone == null)
            return null;

        clone.transform.position = pos;
        return clone;
    }

    protected void CopyActiveObjectsTo(List<IPoolObject> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        foreach (var pooledObject in activeObjects)
        {
            if (pooledObject != null)
                destination.Add(pooledObject);
        }
    }

    protected void ReleaseTrackedActiveObjects()
    {
        CopyActiveObjectsTo(activeObjectBuffer);
        foreach (var pooledObject in activeObjectBuffer)
        {
            pooledObject?.ReleaseSelf();
        }

        activeObjectBuffer.Clear();
    }

    protected void ClearActiveObjectTracking()
    {
        activeObjects.Clear();
        activeObjectBuffer.Clear();
    }

    private void HandleCreate(IPoolObject pooledObject)
    {
        onCreate?.Invoke(pooledObject);
    }

    private void HandleGet(IPoolObject pooledObject)
    {
        if (pooledObject != null)
        {
            activeObjects.Add(pooledObject);

            if (pool != null)
                SpawnerOwnedPoolRegistry.Attach(pooledObject, pool);
        }
        if (resetToDefaultOnGetFromPool)
            ResetPooledObjectToDefaultState(pooledObject);

        onGet?.Invoke(pooledObject);
    }

    private static void ResetPooledObjectToDefaultState(IPoolObject pooledObject)
    {
        if (pooledObject == null)
            return;

        if (pooledObject is Component pooledComponent
            && pooledComponent.TryGetComponent<SavableEntity>(out var savableEntity))
        {
            savableEntity.ResetToDefaultState();
            return;
        }

        if (pooledObject is ISavable savable)
        {
            savable.ResetToDefaultState();
        }
    }

    private void HandleRelease(IPoolObject pooledObject)
    {
        if (pooledObject != null)
        {
            activeObjects.Remove(pooledObject);
            SpawnerOwnedPoolRegistry.Detach(pooledObject);
        }

        onRelease?.Invoke(pooledObject);
    }
}

using UnityEngine;
using UnityEngine.Pool;
using System;
using Cysharp.Threading.Tasks;

public class Spawner<T> : MonoBehaviour where T : UnityEngine.Component, IPoolObject
{
    private bool isInit = false;
    public GameObject prefab;
    public ObjectPool<T> pool;
    
    public Action<T> onGet;
    public Action<T> onRelease;

    protected virtual void Awake()
    {
        // todo: Awake() 타이밍에 필요한 기능 구현
        // 현재 Spawner<T> 상속 클래스 위해 만들어두었음
    }
    
    protected virtual void Start()
    {
        if (prefab == null) return;
        
        SetPool(prefab);
    }
    
    public virtual void SetPool(GameObject prefab, Action<T> getAction = null, Action<T> releaseAction = null, int capacity = 0, int max = 0)
    {
        this.prefab = prefab;
        onGet = getAction;
        onRelease = releaseAction;
        SetPool(capacity, max);
    }
    protected virtual void SetPool(int capacity, int max)
    {
        if (PoolingManager.Instance.GetPool<T>(prefab, null, onGet, onRelease, capacity, max)
            is { } newPool)
        {
            pool = newPool;
            isInit = true;
        }
    }

    public T Spawn()
    {
        if (isInit == false)
        {
            Util.Log($"{gameObject.name}.{nameof(Spawner<T>)}.Spawn: pool is null");
            return null;
        }

        return pool.Get();
    }

    public T Spawn(Vector3 pos)
    {
        T clone = Spawn();
        clone.transform.position = pos;
        return clone;
    }

    private UniTask Clear()
    {
        return UniTask.CompletedTask;
    }
}

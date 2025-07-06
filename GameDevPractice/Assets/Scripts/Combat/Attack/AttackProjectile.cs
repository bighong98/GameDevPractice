using System;
using RPG.Core;
using UnityEngine;

public class AttackProjectile : MonoBehaviour, IPoolObject
{
    [SerializeField] private Health target;
    [SerializeField] private float speed = 8;
    [SerializeField] private float maxLifeTime = 5f;
    
    private float lifeTime;
    private void Update()
    {
        if (target == null) return;

        lifeTime += Time.deltaTime;
        if (lifeTime > maxLifeTime)
        {
            ReleaseSelf();
            return;
        }
        
        transform.LookAt(target.transform.position);
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));
    }

    public void SetTarget(Health newTarget)
    {
        if (newTarget == null) return;
        target = newTarget;
    }

    private Vector3 GetAim() // todo: 로직 최적화/보완
    {
        if (target.GetComponent<CapsuleCollider>() is { } targetColl)
        {
            return target.transform.position + (Vector3.up * targetColl.height / 2);
        }
        return target.transform.position;
    }
    
    public GameObject Origin { get; set; }
    public void OnCreateFromPool()
    {
        
    }

    public void OnGetFromPool()
    {
        lifeTime = 0;
    }

    public void OnReleaseFromPool()
    {
        target = null;
    }

    public void OnDestroyFromPool()
    {
        
    }

    public void ReleaseSelf()
    {
        if (gameObject.activeSelf && Origin != null)
            PoolingManager.Instance.ReleaseFromPool(this);
    }
}

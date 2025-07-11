using System;
using UnityEngine;

public class SimplePooledParticlePlayer : MonoBehaviour, IPoolObject
{
    private bool init;
    private ParticleSystem particle;

    private void Init()
    {
        if (init) return;
        
        particle = GetComponent<ParticleSystem>();
        init = (particle != null);
    }

    private void OnParticleSystemStopped()
    {
        PoolingManager.Instance.ReleaseFromPool(this);
    }

    public GameObject Origin { get; set; }
    public void OnCreateFromPool()
    {
        Init();
    }

    public void OnGetFromPool()
    {
        if (init)
            particle.Play();
    }

    public void OnReleaseFromPool()
    {
        
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

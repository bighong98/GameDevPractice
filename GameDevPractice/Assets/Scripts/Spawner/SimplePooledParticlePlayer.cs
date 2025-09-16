using System;
using UnityEngine;
using TH.Core.Pool;

public class SimplePooledParticlePlayer : MonoBehaviour, IPoolObject
{
    private bool init;
    [SerializeField] private ParticleSystem particle;

    private void Init()
    {
        if (init) return;

        if (particle == null)
        {
            if (GetComponent<ParticleSystem>() is { } getCompoResult)
            {
                particle = getCompoResult;
            }
            else if (Util.FindChild<ParticleSystem>(gameObject, recursive: true) is {} findChildResult)
            {
                particle = findChildResult;
            }
        }
        
        init = (particle != null);
    }

    private void OnParticleSystemStopped()
    {
        // PoolingManager.Instance.ReleaseFromPool(this);
        PoolManager.Instance.ReleaseFromPool(this);
    }

    public GameObject Origin { get; set; }
    // public PoolKey PoolKey { get; set; }

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
        // if (gameObject.activeSelf && Origin != null)
        //     PoolingManager.Instance.ReleaseFromPool(this);
        if (gameObject.activeSelf && Origin != null)
            PoolManager.Instance.ReleaseFromPool(this);
    }
}

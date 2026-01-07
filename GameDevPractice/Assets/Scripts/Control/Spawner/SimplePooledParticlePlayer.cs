using System;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;

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
        PoolManager.Instance.ReleaseFromPool(this, releaseToDefaultContainer: true);
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
            PoolManager.Instance.ReleaseFromPool(this, releaseToDefaultContainer: true);
    }
}

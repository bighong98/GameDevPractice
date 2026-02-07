using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;

public class SimplePooledParticlePlayer : MonoBehaviour, IPoolObject
{
    private bool init;
    [SerializeField] private ParticleSystem particle;
    [SerializeField] private ParticleSystemStoppedRelay relay;

    private void Init()
    {
        if (init) return;

        if (particle == null) 
            particle = Util.FindChild<ParticleSystem>(gameObject, recursive: true);
        if (particle == null) return;

        if (relay == null)
            relay = particle.gameObject.GetOrAddComponent<ParticleSystemStoppedRelay>();
        if (relay == null) return;

        var main = particle.main;
        if (main.stopAction != ParticleSystemStopAction.Callback)
            main.stopAction = ParticleSystemStopAction.Callback;

        relay.Bind(this);
        init = true;
    }

    public GameObject Origin { get; set; }

    public void OnCreateFromPool()
    {
        Init();
    }

    public void OnGetFromPool()
    {
        if (!init)
            Init();

        if (!init) return;

        particle.Play(withChildren: true);
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


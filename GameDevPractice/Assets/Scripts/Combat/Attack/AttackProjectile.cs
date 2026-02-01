using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Combat;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;

using Vector3 = UnityEngine.Vector3;
using TH.Utils;

public class AttackProjectile : MonoBehaviour, IPoolObject
{
    [SerializeField] private Health target;
    [SerializeField] private float speed = 12;
    [SerializeField] private float maxLifeTime = 5f;
    
    private CancellationTokenSource projectileCTS;
    private TimeSpan lifeTimeSpan;
    private bool isLaunched;

    private AttackSource attackSource;
    private ICombatSystem combatSystem;

    public event Action<Vector3> OnHit;
    
    private void Update()
    {
        if (!isLaunched) return;
        
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));
    }

    public void SetProjectile(ICombatSystem combatSys, AttackSource atkSource)
    {
        combatSystem = combatSys;
        attackSource = atkSource;
    }

    public void SetProjectile(AttackSource atkSource)
    {
        attackSource = atkSource;
    }

    public void SetTargetAndShoot(Health newTarget, bool homing)
    {
        if (newTarget == null) return;
        
        target = newTarget;
        transform.LookAt(GetAim());

        ResetProjectileCTS();
        Launch(homing);
    }

    private void ResetProjectileCTS()
    {
        if (projectileCTS is { } cts)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
            cts.Dispose();
        }

        projectileCTS = new CancellationTokenSource();
    }

    private void Launch(bool homing)
    {
        if (homing)
        {
            TrackTargetAsync().Forget();
        }
        WaitForLifeTimeAsync().Forget();
        isLaunched = true;
    }

    private Vector3 GetAim() // todo: 로직 최적화/보완
    {
        if (target.GetComponent<CapsuleCollider>() is { } targetColl)
        {
            return target.transform.position + (Vector3.up * targetColl.height / 2);
        }
        return target.transform.position;
    }

    private async UniTaskVoid WaitForLifeTimeAsync()
    {
        await UniTask.Delay(lifeTimeSpan, DelayType.DeltaTime, delayTiming: PlayerLoopTiming.Update, projectileCTS.Token).SuppressCancellationThrow();
        
        KillSelf();
    }

    private async UniTaskVoid TrackTargetAsync()
    {
        var token = projectileCTS.Token;
        while (!token.IsCancellationRequested)
        {
            transform.LookAt(GetAim());
            await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token).SuppressCancellationThrow();
        }
    }

    private void SetTimeSpan() // 최초 초기화 이후 maxLifeTime이 변동되는 케이스에 대한 처리 없음
    {
        if (lifeTimeSpan == default)
        {
            lifeTimeSpan = TimeSpan.FromSeconds(maxLifeTime);
        }
    }

    private void KillSelf()
    {
        if (projectileCTS is { IsCancellationRequested: false } currCTS)
        {
            currCTS.Cancel();
        }
        ReleaseSelf();
    }

    private void OnTriggerEnter(Collider other)
    {
        //todo: Target이 아닐 때 처리
        //todo: 대상이 사망 상태일 때 처리
        //todo: 논타겟팅/타겟팅 스킬의 투사체일 때 처리

        if (other.TryGetComponent(out IDamageable victim))
        {
            combatSystem.ApplyHit(attackSource.ToRequest(victim));
        }
        this.Log($"OnTriggerEnter(): collided with {other}", Logg.LoggingMode.InProgress);
        OnHit?.Invoke(transform.position);
        KillSelf();
    }

    public GameObject Origin { get; set; }

    public void OnCreateFromPool()
    {
        SetTimeSpan();
    }

    public void OnGetFromPool()
    {
        
    }

    public void OnReleaseFromPool()
    {
        target = null;
        isLaunched = false;
    }

    public void OnDestroyFromPool()
    {
        // if (Util.IsQuitting) return;
        if (projectileCTS == null) return;
        
        if (!projectileCTS.IsCancellationRequested)
            projectileCTS.Cancel();
        projectileCTS.Dispose();
    }

    public void ReleaseSelf()
    {
        // if (gameObject.activeSelf && Origin != null)
        //     PoolingManager.Instance.ReleaseFromPool(this);
        if (gameObject.activeSelf && Origin != null)
            PoolManager.Instance.ReleaseFromPool(this);
    }
}

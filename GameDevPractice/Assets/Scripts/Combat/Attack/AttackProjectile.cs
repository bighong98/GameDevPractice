using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Combat;
using TH.Combat.Service;
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
    private readonly HashSet<IDamageable> hitVictims = new();
    private bool canPierceTargets;
    private int maxPierceTargets;
    private int piercedTargetCount;
    private float maxTravelDistance;
    private Vector3 launchStartPosition;


    public event Action<Vector3> OnHit;
    
    private void Update()
    {
        if (!isLaunched) return;

        transform.Translate(Vector3.forward * (speed * Time.deltaTime));

        if (!IsTravelDistanceExceeded())
        {
            return;
        }

        KillSelf();
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

    public void ConfigurePiercing(bool allowPierce, int maxTargets)
    {
        canPierceTargets = allowPierce;
        maxPierceTargets = Mathf.Max(0, maxTargets);
    }

    public void ConfigureMaxTravelDistance(float maxDistance)
    {
        maxTravelDistance = Mathf.Max(0f, maxDistance);
    }



    public void SetTargetAndShoot(Health newTarget, bool homing)
    {
        if (newTarget == null) return;
        
        target = newTarget;
        transform.LookAt(GetAim());
        launchStartPosition = transform.position;


        ResetProjectileCTS();
        ResetPierceState();

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

    private void ResetPierceState()
    {
        piercedTargetCount = 0;
        hitVictims.Clear();
    }

    private bool IsPierceLimitReached()
    {
        return maxPierceTargets > 0 && piercedTargetCount >= maxPierceTargets;
    }

    private bool IsTravelDistanceExceeded()
    {
        if (maxTravelDistance <= 0f)
        {
            return false;
        }

        float sqrDistance = (transform.position - launchStartPosition).sqrMagnitude;
        return sqrDistance >= maxTravelDistance * maxTravelDistance;
    }



    private void OnTriggerEnter(Collider other)
    {
        //todo: 히트타겟팅/논타겟팅 스킬 투사체일 때 처리

        if (!isLaunched)
        {
            return;
        }

        if (TryResolveDamageable(other, out var victim) && combatSystem != null)
        {
            if (!hitVictims.Add(victim))
            {
                this.Log($"OnTriggerEnter(): duplicate victim '{(victim as Component)?.name ?? "unknown"}', ignored", Logg.LoggingMode.Completed);
                return;
            }

            this.Log($"OnTriggerEnter(): apply hit to '{(victim as Component)?.name ?? "unknown"}'", Logg.LoggingMode.Completed);
            combatSystem.ApplyHit(attackSource.ToRequest(victim, transform.position, hasHitPoint: true));
            OnHit?.Invoke(transform.position);

            piercedTargetCount++;
            this.Log($"OnTriggerEnter(): collided with {other}", Logg.LoggingMode.Completed);

            if (canPierceTargets && !IsPierceLimitReached())
            {
                return;
            }

            KillSelf();
            return;
        }

        this.Log($"OnTriggerEnter(): no damageable resolved from '{other.name}'", Logg.LoggingMode.Completed);
        this.Log($"OnTriggerEnter(): collided with {other}", Logg.LoggingMode.Completed);
        OnHit?.Invoke(transform.position);
        KillSelf();
    }

    private static bool TryResolveDamageable(Collider other, out IDamageable victim)
    {
        if (other == null)
        {
            victim = null;
            return false;
        }

        if (other.TryGetComponent(out victim))
        {
            return true;
        }

        var parentHealth = other.GetComponentInParent<Health>();
        if (parentHealth != null)
        {
            victim = parentHealth;
            return true;
        }

        victim = null;
        return false;
    }

    public GameObject Origin { get; set; }

    public void OnCreateFromPool()
    {
        SetTimeSpan();
    }

    public void OnGetFromPool()
    {
        ResetPierceState();
    }

    public void OnReleaseFromPool()
    {
        target = null;
        isLaunched = false;
        launchStartPosition = default;
        ResetPierceState();
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
        if (!gameObject.activeSelf)
            return;

        if (SpawnerOwnedPoolRegistry.TryRelease(this))
            return;

        if (Origin != null)
            PoolManager.Instance.ReleaseFromPool(this);
    }
}

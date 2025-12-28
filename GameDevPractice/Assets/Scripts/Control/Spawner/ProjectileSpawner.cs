using System;
using TH.Combat;
using TH.Attribute;
using TH.Attribute.Stat;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;
using TH.Resource;

public class ProjectileSpawner : Spawner<AttackProjectile>
{
    [SerializeField] private Health projectileTarget; // serialized for debug
    private bool hasTarget;
    private bool isHoming;
    
    private AttackSource projectileAttackSource; // 투사체에 적용될 AttackSource
    private ICombatSystem combatSystem;
    
    [SerializeField] private GameObject onHitParticlePrefab;

    protected override void Start()
    {
        base.Start();
        combatSystem = ServiceLocator.Get<ICombatSystem>();
    }
    
    // public void InitializeProjectileSpawner(Fighter owner, WeaponTypeSO weaponTypeSO)
    public void InitializeProjectileSpawner(IAttacker owner, WeaponTypeSO weaponTypeSO)
    {
        if (owner is not { } shootingWeaponOwner)
        {
            Logg.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            return;
        }
        
        shootingWeaponOwner.OnTargetChanged += SetTarget;
        shootingWeaponOwner.OnAttack += Shoot;

        if (owner is Component c && c.TryGetComponent(out IStatHolder statHolder))
        {
            if (statHolder.GetStat(GameStats.AD) is { } stat)
            {
                stat.OnStatChanged += () =>
                {
                    SetAttackSource(shootingWeaponOwner, stat.Value);
                };
                SetAttackSource(shootingWeaponOwner, stat.Value);
            }
        }

        // SetAttackSource(shootingWeaponOwner, weaponTypeSO.GetDamage);

        if (weaponTypeSO is { HasImpactEffect: true, GetImpactEffect: { } particlePrefab })
        {
            Logg.Log("Trying to Add PlayOnHitEffect as delegate");
            onHitParticlePrefab = particlePrefab;
            
            onCreate = obj =>
            {
                if (obj is not AttackProjectile projectile) return;
                projectile.SetProjectile(combatSystem, projectileAttackSource);
                projectile.OnHit += PlayOnHitEffect; // todo: 현 구조는 Spawner가 사라지면 PlayOnHitEffect 실행이 불가능함. 오류 발생 가능성이 존재한다면 수정 필요
            };

            onGet = obj =>
            {
                if (obj is not AttackProjectile projectile) return;
                projectile.SetProjectile(projectileAttackSource);
            };
        }
    }

    // private void SetAttackSource(Fighter owner, float damage)
    // {
    //     projectileAttackSource = new AttackSource(owner, damage);
    // }
    
    private void SetAttackSource(IAttacker owner, float damage)
    {
        projectileAttackSource = new AttackSource(owner, damage);
    }

    private void SetTarget(Health target)
    {
        projectileTarget = target;
        hasTarget = (target != null); // target이 null이라면 hasTarget = false
    }
    
    private void Shoot()
    {
        if (!hasTarget) return; // 타겟이 없다면 쏘지 않음
        base.Spawn(transform.position).SetTargetAndShoot(projectileTarget, isHoming);
    }

    private void PlayOnHitEffect(Vector3 pos)
    {
        PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(onHitParticlePrefab, null, pos);
    }
}

using System;
using RPG.Combat;
using RPG.Attribute;
using TH.Combat;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;

public class ProjectileSpawner : Spawner<AttackProjectile>
{
    [SerializeField] private Health projectileTarget; // serialized for debug
    private bool hasTarget;
    private bool isHoming;
    
    private AttackSource projectileAttackSource; // 투사체에 적용될 AttackSource
    private ICombatSystem combatSystem;
    
    [SerializeField] private GameObject onHitParticlePrefab;
    // private bool hasOnHitEffect;

    protected override void Start()
    {
        base.Start();
        if (ServiceLocator.TryGet(out ICombatSystem combat))
        {
            combatSystem = combat;
        }
    }
    
    public void InitializeProjectileSpawner(Fighter owner, WeaponTypeSO weaponTypeSO)
    {
        if (owner is not { } shootingWeaponOwner)
        {
            Logg.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            return;
        }
        
        shootingWeaponOwner.OnTargetChanged += SetTarget;
        shootingWeaponOwner.OnAttack += Shoot;

        projectileAttackSource = new AttackSource(shootingWeaponOwner, weaponTypeSO.GetDamage);

        if (weaponTypeSO is { HasImpactEffect: true, GetImpactEffect: { } particlePrefab })
        {
            Logg.Log("Trying to Add PlayOnHitEffect as delegate");
            // hasOnHitEffect = true;
            onHitParticlePrefab = particlePrefab;
            
            onCreate = obj =>
            {
                // obj.OnHit += PlayOnHitEffect; // todo: 현 구조는 Spawner가 사라지면 PlayOnHitEffect 실행이 불가능함. 오류 발생 가능성이 존재한다면 수정 필요
                if (obj is AttackProjectile projectile)
                {
                    projectile.SetProjectile(combatSystem, projectileAttackSource);
                    projectile.OnHit += PlayOnHitEffect; // todo: 현 구조는 Spawner가 사라지면 PlayOnHitEffect 실행이 불가능함. 오류 발생 가능성이 존재한다면 수정 필요
                }
            };

            onGet = obj =>
            {
                if (obj is AttackProjectile projectile)
                {
                    projectile.SetProjectile(projectileAttackSource);
                }
            };
        }
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
        // PoolingManager.Instance.GetFromPool<SimplePooledParticlePlayer>(onHitParticlePrefab, null, pos);
    }
}

using System;
using RPG.Combat;
using RPG.Attribute;
using UnityEngine;

public class ProjectileSpawner : Spawner<AttackProjectile>
{
    [SerializeField] private Health projectileTarget; // serialized for debug
    private bool hasTarget;
    private bool isHoming;

    [SerializeField] private GameObject onHitParticlePrefab;
    // private bool hasOnHitEffect;
    
    public void InitializeProjectileSpawner(Fighter owner, WeaponTypeSO weaponTypeSO)
    {
        if (owner is not { } shootingWeaponOwner)
        {
            Util.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            return;
        }
        
        shootingWeaponOwner.OnTargetChanged += SetTarget;
        shootingWeaponOwner.OnAttack += Shoot;

        if (weaponTypeSO is { HasImpactEffect: true, GetImpactEffect: { } particlePrefab })
        {
            Util.Log("Trying to Add PlayOnHitEffect as delegate");
            // hasOnHitEffect = true;
            onHitParticlePrefab = particlePrefab;

            onCreate = obj =>
            {
                obj.OnHit += PlayOnHitEffect; // todo: 현 구조는 Spawner가 사라지면 PlayOnHitEffect 실행이 불가능함. 오류 발생 가능성이 존재한다면 수정 필요
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
        base.Spawn(transform.position).SetTarget(projectileTarget, isHoming);
    }

    private void PlayOnHitEffect(Vector3 pos)
    {
        PoolingManager.Instance.GetFromPool<SimplePooledParticlePlayer>(onHitParticlePrefab, pos);
    }
}

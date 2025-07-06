using System;
using RPG.Combat;
using RPG.Core;
using UnityEngine;

public class ProjectileSpawner : Spawner<AttackProjectile>
{
    [SerializeField] private Health projectileTarget; // serialized for debug
    private bool hasTarget;
    
    public void InitializeProjectileSpawner(Fighter owner)
    {
        if (owner is not { } shootingWeaponOwner)
        {
            Util.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            return;
        }
        shootingWeaponOwner.OnTargetChanged += SetTarget;
        shootingWeaponOwner.OnAttack += Shoot;
    }

    private void SetTarget(Health target)
    {
        projectileTarget = target;
        hasTarget = (target != null); // target이 null이라면 hasTarget = false
    }
    
    private void Shoot()
    {
        if (!hasTarget) return; // 타겟이 없다면 쏘지 않음
        base.Spawn(transform.position).SetTarget(projectileTarget);
    }
}

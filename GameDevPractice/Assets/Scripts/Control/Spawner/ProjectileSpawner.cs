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
    private IAttacker currentOwner;
    private Fighter fighterOwner;
    private IStatHolder ownerStatHolder;
    private GameStatSO attackSourceStatSO;

    [SerializeField] private GameObject onHitParticlePrefab;

    private const string AllyLayerName = "Ally";
    private const string EnemyLayerName = "Enemy";
    private const string AllyAttackLayerName = "AllyAttack";
    private const string EnemyAttackLayerName = "EnemyAttack";

    private int projectileLayer = -1;

    protected override void Start()
    {
        base.Start();
        combatSystem = ServiceLocator.Get<ICombatSystem>();
    }
    
    public void InitializeProjectileSpawner(IAttacker owner, WeaponTypeSO weaponTypeSO)
    {
        combatSystem ??= ServiceLocator.Get<ICombatSystem>();

        if (owner is not { } shootingWeaponOwner)
        {
            Logg.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            UnbindOwner();
            return;
        }
        
        BindOwner(shootingWeaponOwner, weaponTypeSO);

        if (weaponTypeSO is { HasImpactEffect: true, GetImpactEffect: { } particlePrefab })
        {
            Logg.Log("Trying to Add PlayOnHitEffect as delegate");
            onHitParticlePrefab = particlePrefab;
        }
        else
        {
            onHitParticlePrefab = null;
        }

        onCreate = obj =>
        {
            if (obj is not AttackProjectile projectile) return;
            projectile.SetProjectile(combatSystem, projectileAttackSource);
            ApplyProjectileLayer(projectile);
            projectile.OnHit -= PlayOnHitEffect;
            if (onHitParticlePrefab != null)
                projectile.OnHit += PlayOnHitEffect;
        };

        onGet = obj =>
        {
            if (obj is not AttackProjectile projectile) return;
            projectile.SetProjectile(projectileAttackSource);
            ApplyProjectileLayer(projectile);
            projectile.OnHit -= PlayOnHitEffect;
            if (onHitParticlePrefab != null)
                projectile.OnHit += PlayOnHitEffect;
        };
    }

    private void OnDisable()
    {
        UnbindOwner();
    }

    private void BindOwner(IAttacker owner, WeaponTypeSO weaponTypeSO)
    {
        UnbindOwner();
        currentOwner = owner;

        if (currentOwner.IsNull()) return;

        projectileLayer = ResolveProjectileLayer(currentOwner);

        currentOwner.OnTargetSet += SetTarget;
        currentOwner.OnAttack += Shoot;

        if (currentOwner is Fighter fighter)
        {
            fighterOwner = fighter;
            fighterOwner.OnAttackSourceChanged += OnOwnerAttackSourceChanged;
            projectileAttackSource = fighterOwner.CurrentAttackSource;
            return;
        }

        if (currentOwner is not Component c || !c.TryGetComponent(out IStatHolder statHolder)) return;

        var statSO = weaponTypeSO != null ? weaponTypeSO.AttackSourceStatSO : null;
        if (statSO.IsNull())
            statSO = GameStats.AD;

        if (statSO.IsNull()) return;

        ownerStatHolder = statHolder;
        attackSourceStatSO = statSO;
        ownerStatHolder.BindStatChanged(attackSourceStatSO, OnAttackStatChanged);
    }

    private void UnbindOwner()
    {
        if (currentOwner.IsNotNull())
        {
            currentOwner.OnTargetSet -= SetTarget;
            currentOwner.OnAttack -= Shoot;
        }

        if (fighterOwner != null)
        {
            fighterOwner.OnAttackSourceChanged -= OnOwnerAttackSourceChanged;
            fighterOwner = null;
        }

        if (ownerStatHolder.IsNotNull() && attackSourceStatSO.IsNotNull())
            ownerStatHolder.UnbindStatChanged(attackSourceStatSO, OnAttackStatChanged);

        ownerStatHolder = null;
        attackSourceStatSO = null;

        currentOwner = null;
        projectileLayer = -1;
        hasTarget = false;
        projectileTarget = null;
    }

    private int ResolveProjectileLayer(IAttacker owner)
    {
        if (owner is not Component ownerComponent) return -1;

        var ownerLayer = ownerComponent.gameObject.layer;
        var ownerLayerName = LayerMask.LayerToName(ownerLayer);

        var targetLayerName = ownerLayerName switch
        {
            AllyLayerName => AllyAttackLayerName,
            EnemyLayerName => EnemyAttackLayerName,
            _ => ownerLayerName
        };

        var targetLayer = LayerMask.NameToLayer(targetLayerName);
        return targetLayer >= 0 ? targetLayer : ownerLayer;
    }

    private void ApplyProjectileLayer(AttackProjectile projectile)
    {
        if (projectile == null) return;
        var layer = projectileLayer;
        if (layer < 0 && currentOwner.IsNotNull())
            layer = ResolveProjectileLayer(currentOwner);
        if (layer < 0) return;

        SetLayerRecursively(projectile.gameObject, layer);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    
    private void OnAttackStatChanged(float value)
    {
        if (currentOwner.IsNull()) return;
        SetAttackSource(currentOwner, value);
    }

    private void OnOwnerAttackSourceChanged(AttackSource source)
    {
        projectileAttackSource = source;
    }
    
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
        if (!hasTarget) return; // ?寃잛씠 ?녿떎硫??섏? ?딆쓬

        combatSystem ??= ServiceLocator.Get<ICombatSystem>();
        var projectile = base.Spawn(transform.position);
        if (projectile == null) return;

        ApplyProjectileLayer(projectile);
        projectile.OnHit -= PlayOnHitEffect;
        if (onHitParticlePrefab != null)
            projectile.OnHit += PlayOnHitEffect;

        // Ensure latest attack source is applied before launch.
        projectile.SetProjectile(combatSystem, projectileAttackSource);
        projectile.SetTargetAndShoot(projectileTarget, isHoming);
    }

    private void PlayOnHitEffect(Vector3 pos)
    {
        PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(onHitParticlePrefab, null, pos);
    }
}

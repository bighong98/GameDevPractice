using TH.Attribute;
using TH.Combat;
using TH.Combat.Service;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

public class ProjectileSpawner : Spawner<AttackProjectile>, ISkillProjectileExecutor
{
    [SerializeField] private Health projectileTarget;
    [SerializeField] private SkillTargetLayerMapSO skillTargetLayerMap;
    private bool hasTarget;
    private bool isHoming;

    private ICombatSystem combatSystem;
    private AttackSource projectileAttackSource;

    private IAttacker currentOwner;
    private SkillTargetLayerMaskResolver projectileLayerResolver;

    private int projectileLayer = -1;

    protected override void Start()
    {
        base.Start();
        combatSystem = ServiceLocator.Get<ICombatSystem>();
    }

    public void InitializeProjectileSpawner(IAttacker owner, SkillTypeSO skillTypeSO, AttackSource attackSource)
    {
        _ = skillTypeSO;
        combatSystem ??= ServiceLocator.Get<ICombatSystem>();

        if (owner is not { } shootingWeaponOwner)
        {
            Logg.Log($"[{name}.{nameof(ProjectileSpawner)}] failed to {nameof(InitializeProjectileSpawner)}");
            UnbindOwner();
            return;
        }

        projectileAttackSource = attackSource;
        BindOwner(shootingWeaponOwner);

        onCreate = obj =>
        {
            if (obj is not AttackProjectile projectile) return;

            projectile.SetProjectile(combatSystem, projectileAttackSource);
            ApplyProjectileLayer(projectile);
        };

        onGet = obj =>
        {
            if (obj is not AttackProjectile projectile) return;

            projectile.SetProjectile(projectileAttackSource);
            ApplyProjectileLayer(projectile);
        };
    }

    public bool TryExecuteProjectile(in AttackSource attackSource, Health target, SkillTypeSO skill)
    {
        _ = skill;
        projectileAttackSource = attackSource;

        if (target != null)
            SetTarget(target);

        return Shoot();
    }

    private void OnDisable()
    {
        UnbindOwner();
    }

    private void BindOwner(IAttacker owner)
    {
        UnbindOwner();
        if (owner.IsNull()) return;

        currentOwner = owner;
        ConfigureLayerResolver(currentOwner);
        projectileLayer = ResolveProjectileLayer(currentOwner);
        currentOwner.OnTargetSet += SetTarget;
    }

    private void UnbindOwner()
    {
        if (currentOwner.IsNotNull())
        {
            currentOwner.OnTargetSet -= SetTarget;
        }

        currentOwner = null;
        projectileLayer = -1;
        hasTarget = false;
        projectileTarget = null;
    }

    private int ResolveProjectileLayer(IAttacker owner)
    {
        projectileLayerResolver ??= new SkillTargetLayerMaskResolver(skillTargetLayerMap);
        return projectileLayerResolver.ResolveProjectileLayer(owner);
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

    private void SetTarget(Health target)
    {
        projectileTarget = target;
        hasTarget = target != null;
    }

    private bool Shoot()
    {
        if (!hasTarget) return false;

        combatSystem ??= ServiceLocator.Get<ICombatSystem>();
        var projectile = base.Spawn(transform.position);
        if (projectile == null) return false;

        ApplyProjectileLayer(projectile);
        projectile.SetProjectile(combatSystem, projectileAttackSource);
        projectile.SetTargetAndShoot(projectileTarget, isHoming);
        return true;
    }

    private void ConfigureLayerResolver(IAttacker owner)
    {
        if (skillTargetLayerMap == null &&
            owner is Component ownerComponent &&
            ownerComponent.TryGetComponent<SkillController>(out var skillController))
        {
            skillTargetLayerMap = skillController.SkillTargetLayerMap;
        }

        projectileLayerResolver = new SkillTargetLayerMaskResolver(skillTargetLayerMap);
    }
}

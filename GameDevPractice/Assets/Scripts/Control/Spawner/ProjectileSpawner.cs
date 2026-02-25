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
    private bool projectilePierceTargets;
    private int projectileMaxPierceTargets;
    private float projectileMaxTravelDistance;


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
        ApplyProjectileSkillOptions(skillTypeSO.IsNotNull() ? new GameSkill(skillTypeSO) : null);
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
            projectile.ConfigurePiercing(projectilePierceTargets, projectileMaxPierceTargets);
            projectile.ConfigureMaxTravelDistance(projectileMaxTravelDistance);
            ApplyProjectileLayer(projectile);
        };

        onGet = obj =>
        {
            if (obj is not AttackProjectile projectile) return;

            projectile.SetProjectile(projectileAttackSource);
            projectile.ConfigurePiercing(projectilePierceTargets, projectileMaxPierceTargets);
            projectile.ConfigureMaxTravelDistance(projectileMaxTravelDistance);
            ApplyProjectileLayer(projectile);
        };
    }

    public bool TryExecuteProjectile(in AttackSource attackSource, Health target, IGameSkill skill)
    {
        if (skill.IsNull() || !skill.HasProjectile || skill.ProjectilePrefab.IsNull())
        {
            return false;
        }

        var ownerComponent = currentOwner as Component;
        string ownerName = ownerComponent != null ? ownerComponent.name : "null";
        string targetName = target != null ? target.name : "null";
        string skillName = skill != null ? skill.Name : "null";

        this.Log(
            $"[{nameof(ProjectileSpawner)}.{nameof(TryExecuteProjectile)}] owner={ownerName}, frame={Time.frameCount}, time={Time.time:0.000}, " +
            $"skill={skillName}, attackId={attackSource.AttackInstanceId}, target={targetName}",
            Logg.LoggingMode.Completed);

        projectileAttackSource = attackSource;
        ApplyProjectileSkillOptions(skill);
        if (!EnsurePoolForSkill(skill))
        {
            return false;
        }

        if (target != null)
            SetTarget(target);

        bool shot = Shoot();
        this.Log(
            $"[{nameof(ProjectileSpawner)}.{nameof(TryExecuteProjectile)}] owner={ownerName}, result={(shot ? "shot" : "failed")}, " +
            $"attackId={attackSource.AttackInstanceId}",
            Logg.LoggingMode.Completed);
        return shot;
    }

    private bool EnsurePoolForSkill(IGameSkill skill)
    {
        if (skill.IsNull() || skill.ProjectilePrefab.IsNull())
        {
            return false;
        }

        if (!HasPool || Prefab != skill.ProjectilePrefab)
        {
            SetPool(skill.ProjectilePrefab);
        }

        return HasPool;
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

    private void ApplyProjectileSkillOptions(IGameSkill skill)
    {
        if (skill.IsNull())
        {
            projectilePierceTargets = false;
            projectileMaxPierceTargets = 0;
            projectileMaxTravelDistance = 0f;
            return;
        }

        projectilePierceTargets = skill.ProjectilePierceTargets;
        projectileMaxPierceTargets = skill.ProjectileMaxPierceTargets;
        projectileMaxTravelDistance = skill.ProjectileMaxTravelDistance;
    }


    private bool Shoot()
    {
        if (!hasTarget) return false;

        combatSystem ??= ServiceLocator.Get<ICombatSystem>();
        var projectile = base.Spawn(transform.position);
        if (projectile == null) return false;

        ApplyProjectileLayer(projectile);
        projectile.SetProjectile(combatSystem, projectileAttackSource);
        projectile.ConfigurePiercing(projectilePierceTargets, projectileMaxPierceTargets);
        projectile.ConfigureMaxTravelDistance(projectileMaxTravelDistance);
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

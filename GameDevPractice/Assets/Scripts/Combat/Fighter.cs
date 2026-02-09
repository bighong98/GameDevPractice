using System;
using TH.Attribute;
using TH.Combat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

public interface IFighter : IAttacker { }

public class Fighter : MonoBehaviour, IFighter
{
    [SerializeField] private Health target;

    public event Action OnAttack;
    public event Action<Health> OnTargetSet;
    public event Action OnAttackReady;

    public bool IsTargetInRange
    {
        get
        {
            if (!IsTargetValid) return false;
            if (skillController.IsNull() || !skillController.HasActiveSkill) return false;

            return Vector3.Distance(transform.position, target.transform.position) <= skillController.ActiveSkillRange;
        }
    }

    public bool IsTargetValid => target.IsNotNull() && !target.IsDead;
    public Health Target => target;

    private ICombatSystem combatSystem;
    private ISkillController skillController;

    private AttackSource currAttackSource;
    private AudioClip currAttackSfx;

    private void Awake()
    {
        combatSystem = ServiceLocator.Get<ICombatSystem>();

        if (!TryGetComponent(out skillController))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No ISkillController found");
    }

    private void Start()
    {
        RefreshPreviewAttackSource();
    }

    private void OnEnable()
    {
        if (skillController.IsNotNull())
            skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
    }

    private void OnDisable()
    {
        if (skillController.IsNotNull())
            skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
    }

    private void Update()
    {
        if (!IsTargetValid) return;
        if (skillController.IsNull() || !skillController.HasActiveSkill) return;
        if (!skillController.IsActiveSkillReady) return;

        OnAttackReady?.Invoke();
    }

    #region IAttackable

    public void Attack()
    {
        if (skillController.IsNull()) return;
        currAttackSfx = null;
        if (!skillController.TryConsumeActiveSkill(this, out currAttackSource)) return;

        currAttackSfx = skillController.ResolvedSkillSFX;
    }

    public void SetTarget(Health attackTarget)
    {
        target = attackTarget;
        OnTargetSet?.Invoke(target);
    }

    public bool CanAttack(GameObject attackTarget, out Health targetHealth)
    {
        if (attackTarget == null || attackTarget == gameObject)
        {
            targetHealth = null;
            return false;
        }

        if (attackTarget.GetComponent<Health>() is { } health)
        {
            targetHealth = health;
            return true;
        }

        targetHealth = null;
        return false;
    }

    #endregion

    #region Animation Event Method

    void Hit()
    {
        if (!IsTargetValid) return;

        Shoot();
        combatSystem.ApplyHit(currAttackSource.ToRequest(target));
    }

    void Shoot()
    {
        var attackSfx = ResolveCurrentAttackSfx();
        if (attackSfx != null)
            SoundManager.Instance.Play(Enums.AudioType.Effect, attackSfx);

        OnAttack?.Invoke();
    }

    #endregion

    private void HandleActiveSkillChanged(SkillTypeSO _)
    {
        RefreshPreviewAttackSource();
    }

    private void RefreshPreviewAttackSource()
    {
        if (skillController.IsNull()) return;

        if (skillController.TryBuildPreviewAttackSource(this, out var previewAttackSource))
            currAttackSource = previewAttackSource;
    }

    private AudioClip ResolveCurrentAttackSfx()
    {
        if (currAttackSfx != null)
            return currAttackSfx;

        if (skillController.IsNotNull() && skillController.HasResolvedSkill)
            return skillController.ResolvedSkillSFX;

        if (skillController.IsNotNull() && skillController.HasActiveSkill)
            return skillController.ActiveSkillSFX;

        return null;
    }
}

using System;
using TH.Attribute;
using TH.Combat;
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

    private ISkillController skillController;
    private AudioClip currAttackSfx;

    private void Awake()
    {
        if (!TryGetComponent(out skillController))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No ISkillController found");
    }

    private void Update()
    {
        if (!IsTargetValid) return;
        if (skillController.IsNull() || !skillController.HasActiveSkill) return;
        if (!skillController.IsActiveSkillReady) return;

        OnAttackReady?.Invoke();
    }

    public void Attack()
    {
        if (skillController.IsNull()) return;

        currAttackSfx = null;
        if (!skillController.TryConsumeActiveSkill(this, out _)) return;

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

    void Hit()
    {
        TriggerAttack();
    }

    void Shoot()
    {
        TriggerAttack();
    }

    private void TriggerAttack()
    {
        if (!IsTargetValid) return;
        if (skillController.IsNull()) return;
        if (!skillController.TryExecutePendingAttack(this, target)) return;

        var attackSfx = ResolveCurrentAttackSfx();
        if (attackSfx != null)
            SoundManager.Instance.Play(Enums.AudioType.Effect, attackSfx);

        OnAttack?.Invoke();
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

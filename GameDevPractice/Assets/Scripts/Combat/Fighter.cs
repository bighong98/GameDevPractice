using System;
using System.Diagnostics;
using TH.Attribute;
using TH.Combat;
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

        bool consumed = skillController.TryConsumeActiveSkill(this, out _);
        LogAttackFlow("Attack", consumed);
    }

    public void SetTarget(Health attackTarget)
    {
        if (ReferenceEquals(target, attackTarget))
        {
            return;
        }

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
        bool executed = skillController.TryExecutePendingAttack(this, target);
        LogAttackFlow("TriggerAttack", executed);
        if (!executed) return;

        OnAttack?.Invoke();
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    private void LogAttackFlow(string stage, bool result)
    {
        string targetName = target.IsNotNull() ? target.name : "null";
        string activeName = skillController != null && skillController.HasActiveSkill && skillController.ActiveSkill.IsNotNull()
            ? skillController.ActiveSkill.name
            : "null";
        string resolvedName = skillController != null && skillController.HasResolvedSkill && skillController.ResolvedSkill.IsNotNull()
            ? skillController.ResolvedSkill.name
            : "null";
        string executingName = skillController != null && skillController.HasExecutingSkill && skillController.ExecutingSkill.IsNotNull()
            ? skillController.ExecutingSkill.name
            : "null";

        Logg.Log(
            $"[{nameof(Fighter)}.{stage}] owner={name}, frame={Time.frameCount}, time={Time.time:0.000}, result={result}, " +
            $"target={targetName}, active={activeName}, resolved={resolvedName}, executing={executingName}",
            Logg.LoggingMode.Completed);
    }
}

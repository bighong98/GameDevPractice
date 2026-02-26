using System;
using System.Diagnostics;
using TH.Attribute;
using TH.Combat;
using TH.Resource;
using TH.Utils;
using TH.Control.Movement;
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

            float range = Mathf.Max(0f, skillController.ActiveSkillRange) + RangeCompareBuffer;
            float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
            return sqrDistance <= range * range;
        }
    }

    public bool IsTargetValid => IsTargetUsable(target);
    public Health Target => target;

    private ISkillController skillController;
    private Animator animator;
    private CombatTarget subscribedCombatTarget;
    private Health pendingExecutionTarget;
    private bool pendingStaleWatchActive;
    private int pendingStaleWatchStartFrame = -1;
    private float pendingStaleWatchStartTime = -1f;
    private float nextAttackReadyBlockedLogTime;
    private const float AttackReadyBlockedLogInterval = 0.5f;
    private const float RangeCompareBuffer = 0.1f;
    private const float PendingStaleMinDuration = 0.2f;
    private const float PendingStaleNoAnimatorDuration = 0.8f;
    private const int PendingStaleMinFrames = 8;
    private const int AnimatorBaseLayer = 0;
    private static readonly int AttackStateHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        if (!TryGetComponent(out skillController))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No ISkillController found");

        if (!TryGetComponent(out animator))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No Animator found");
    }

    private void OnDisable()
    {
        UnsubscribeTargetInvalidation();
        DisarmPendingStaleWatch();
    }

    private void Update()
    {
        TryCancelStalePendingAttack();

        if (!IsTargetValid) return;
        if (skillController.IsNull() || !skillController.HasActiveSkill) return;

        if (!skillController.IsActiveSkillReady)
        {
            LogAttackReadyBlocked("update_not_ready");
            return;
        }

        OnAttackReady?.Invoke();
    }

    public void Attack()
    {
        if (skillController.IsNull()) return;

        bool consumed = skillController.TryConsumeActiveSkill(this, out _);
        if (consumed)
        {
            CacheExecutionTargetSnapshot();
            ArmPendingStaleWatch();
        }
        else
        {
            ClearExecutionTargetSnapshot();
            SyncPendingStaleWatchState();
        }

        LogAttackFlow("Attack", consumed);
    }

    public void SetTarget(Health attackTarget, bool forceNotify = false)
    {
        if (ReferenceEquals(target, attackTarget) && !forceNotify)
        {
            return;
        }

        UnsubscribeTargetInvalidation();
        target = attackTarget;
        SubscribeTargetInvalidation(target);
        OnTargetSet?.Invoke(target);
        LogAttackFlow(forceNotify ? "SetTarget_force" : "SetTarget", IsTargetUsable(target));
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
        if (skillController.IsNull())
        {
            ClearExecutionTargetSnapshot();
            DisarmPendingStaleWatch();
            LogAttackFlow("TriggerAttack_blocked_no_skill_controller", false);
            return;
        }

        var executionTarget = ResolveExecutionTarget();
        if (executionTarget.IsNull() || executionTarget.IsDead)
        {
            bool canceled = skillController.TryExecutePendingAttack(this, null);
            ClearExecutionTargetSnapshot();
            SyncPendingStaleWatchState();
            LogAttackFlow("TriggerAttack_blocked_invalid_target", canceled);
            return;
        }

        bool executed = skillController.TryExecutePendingAttack(this, executionTarget);
        ClearExecutionTargetSnapshot();
        SyncPendingStaleWatchState();
        LogAttackFlow("TriggerAttack", executed);
        if (!executed) return;

        OnAttack?.Invoke();
    }

    #region For Debug

#if UNITY_EDITOR
    [SerializeField] private bool logAttackFlow = false;
#endif
    

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    private void LogAttackReadyBlocked(string stage)
    {
        if (!logAttackFlow) return;
        if (Time.time < nextAttackReadyBlockedLogTime)
        {
            return;
        }

        nextAttackReadyBlockedLogTime = Time.time + AttackReadyBlockedLogInterval;

        float range = skillController != null ? skillController.ActiveSkillRange : 0f;
        float distance = target.IsNotNull() ? Vector3.Distance(transform.position, target.transform.position) : -1f;

        Logg.Log(
            $"[{nameof(Fighter)}.{stage}] owner={name}, frame={Time.frameCount}, time={Time.time:0.000}, " +
            $"targetValid={IsTargetValid}, inRange={IsTargetInRange}, distance={distance:0.###}, range={range:0.###}, " +
            $"activeReady={(skillController != null && skillController.IsActiveSkillReady)}",
            Logg.LoggingMode.InProgress, context: this);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    private void LogAttackFlow(string stage, bool result)
    {
        if (!logAttackFlow) return;
        string targetName = target.IsNotNull() ? target.name : "null";
        string snapshotTargetName = pendingExecutionTarget.IsNotNull() ? pendingExecutionTarget.name : "null";
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
            $"target={targetName}, snapshotTarget={snapshotTargetName}, active={activeName}, resolved={resolvedName}, executing={executingName}",
            Logg.LoggingMode.InProgress, context: this);
    }

    #endregion

    private void CacheExecutionTargetSnapshot()
    {
        pendingExecutionTarget = IsTargetValid ? target : null;
    }

    private Health ResolveExecutionTarget()
    {
        if (IsTargetUsable(pendingExecutionTarget))
        {
            return pendingExecutionTarget;
        }

        return null;
    }

    private void ClearExecutionTargetSnapshot()
    {
        pendingExecutionTarget = null;
    }

    private void TryCancelStalePendingAttack()
    {
        if (skillController.IsNull()) return;

        if (!skillController.HasPendingAttack)
        {
            DisarmPendingStaleWatch();
            return;
        }

        if (!pendingStaleWatchActive)
        {
            ArmPendingStaleWatch();
            return;
        }

        int elapsedFrames = Time.frameCount - pendingStaleWatchStartFrame;
        float elapsedTime = Time.time - pendingStaleWatchStartTime;
        if (elapsedFrames < PendingStaleMinFrames || elapsedTime < PendingStaleMinDuration)
        {
            return;
        }

        bool shouldCancel;
        if (animator.IsNotNull())
        {
            shouldCancel = !IsAnimatorInAttackPhase();
        }
        else
        {
            shouldCancel = elapsedTime >= PendingStaleNoAnimatorDuration;
        }

        if (!shouldCancel)
        {
            return;
        }

        bool canceled = skillController.TryCancelPendingAttackIfStale();
        if (!canceled)
        {
            return;
        }

        ClearExecutionTargetSnapshot();
        DisarmPendingStaleWatch();
        LogAttackFlow("stale_pending_canceled", true);
    }

    private bool IsAnimatorInAttackPhase()
    {
        if (animator.IsNull() || !animator.isActiveAndEnabled)
        {
            return false;
        }

        if (animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash == AttackStateHash)
        {
            return true;
        }

        if (!animator.IsInTransition(AnimatorBaseLayer))
        {
            return false;
        }

        return animator.GetNextAnimatorStateInfo(AnimatorBaseLayer).shortNameHash == AttackStateHash;
    }

    private void ArmPendingStaleWatch()
    {
        pendingStaleWatchActive = true;
        pendingStaleWatchStartFrame = Time.frameCount;
        pendingStaleWatchStartTime = Time.time;
    }

    private void DisarmPendingStaleWatch()
    {
        pendingStaleWatchActive = false;
        pendingStaleWatchStartFrame = -1;
        pendingStaleWatchStartTime = -1f;
    }

    private void SyncPendingStaleWatchState()
    {
        if (skillController.IsNotNull() && skillController.HasPendingAttack)
        {
            return;
        }

        DisarmPendingStaleWatch();
    }

    private void SubscribeTargetInvalidation(Health candidate)
    {
        if (candidate.IsNull() || !candidate.TryGetComponent(out CombatTarget combatTarget))
        {
            subscribedCombatTarget = null;
            return;
        }

        subscribedCombatTarget = combatTarget;
        subscribedCombatTarget.OnInvalidated += HandleTargetInvalidated;
    }

    private void UnsubscribeTargetInvalidation()
    {
        if (subscribedCombatTarget.IsNull())
        {
            return;
        }

        subscribedCombatTarget.OnInvalidated -= HandleTargetInvalidated;
        subscribedCombatTarget = null;
    }

    private void HandleTargetInvalidated(CombatTarget invalidatedTarget)
    {
        if (target.IsNull() || invalidatedTarget.IsNull())
        {
            return;
        }

        if (!ReferenceEquals(invalidatedTarget.gameObject, target.gameObject))
        {
            return;
        }

        SetTarget(null);
        ClearExecutionTargetSnapshot();
        SyncPendingStaleWatchState();
        LogAttackFlow("TargetInvalidated", true);
    }

    private static bool IsTargetUsable(Health candidate)
    {
        return candidate.IsNotNull() &&
               !candidate.IsDead &&
               candidate.gameObject.activeInHierarchy;
    }
}

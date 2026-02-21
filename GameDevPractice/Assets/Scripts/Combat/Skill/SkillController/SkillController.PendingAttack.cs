using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    // 스킬 소비/보류 공격 실행 상태 관리 파트
    public sealed partial class SkillController
    {
        // 활성 스킬 소비 + 공격 소스 생성 + 보류 상태 적재
        public bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;

            // 활성 스킬 미보유 또는 공격자 미유효 가드
            if (!HasActiveSkill || attacker.IsNull()) return false;
            LogConsumeState("begin");

            // 보류 공격 존재 시 재사용 우선 경로
            if (hasPendingAttack)
            {
                if (!attacker.IsNull() &&
                    ReferenceEquals(pendingAttacker, attacker) &&
                    IsPendingAttackReusableState())
                {
                    LogConsumeState("blocked_by_pending_attack");
                    return false;
                }

                // 재사용 실패 보류 상태 정리
                LogConsumeState("reuse_pending_failed_before_cancel");
                CancelPendingAttack(PendingCancelReason.InvalidatedOnConsume, refreshResolvedFromPreview: true);
                LogConsumeState("reuse_pending_failed_after_cancel");
            }

            var baseSkill = skillBook.ActiveSkill;
            // 쿨다운 미준비 가드
            if (!skillCaster.IsReady(baseSkill))
            {
                LogConsumeState("blocked_not_ready", baseSkill: baseSkill);
                return false;
            }
            // 현재 콤보 단계 기준 해석 실패 가드
            if (!TryResolveSkillPreview(baseSkill, out var resolved, out var stepIndex, out var stepCount))
            {
                LogConsumeState("resolve_preview_failed", baseSkill: baseSkill);
                return false;
            }

            LogConsumeState("resolved_preview", baseSkill, resolved, stepIndex, stepCount);
            SetPendingAttack(attacker, resolved, baseSkill, stepIndex, stepCount);
            executingSkill = resolved;
            LogConsumeState("pending_armed", baseSkill, resolved, stepIndex, stepCount);
            UpdateResolvedSkillFromPreview(forceNotify: true);
            LogConsumeState("after_resolved_update", baseSkill, resolved, stepIndex, stepCount);
            SyncDebugValues();
            return true;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void LogConsumeState(
            string stage,
            SkillTypeSO baseSkill = null,
            SkillTypeSO resolvedPreviewSkill = null,
            int stepIndex = -1,
            int stepCount = -1,
            int attackInstanceId = 0)
        {
            string activeSkillName = skillBook != null && skillBook.ActiveSkill.IsNotNull() ? skillBook.ActiveSkill.name : "null";
            string resolvedSkillName = resolvedSkill.IsNotNull() ? resolvedSkill.name : "null";
            string pendingSkillName = pendingAttackSkill.IsNotNull() ? pendingAttackSkill.name : "null";
            string pendingBaseName = pendingBaseSkill.IsNotNull() ? pendingBaseSkill.name : "null";
            string baseSkillName = baseSkill.IsNotNull() ? baseSkill.name : "null";
            string resolvedPreviewName = resolvedPreviewSkill.IsNotNull() ? resolvedPreviewSkill.name : "null";
            string ownerName = gameObject != null ? gameObject.name : "null";
            int ownerInstanceId = gameObject != null ? gameObject.GetInstanceID() : 0;

            Logg.Log(
                $"[{nameof(SkillController)}.{nameof(TryConsumeActiveSkill)}] " +
                $"owner={ownerName}#{ownerInstanceId}, " +
                $"stage={stage}, frame={Time.frameCount}, time={Time.time:0.000}, " +
                $"active={activeSkillName}, resolvedCurrent={resolvedSkillName}, " +
                $"baseArg={baseSkillName}, resolvedArg={resolvedPreviewName}, step={stepIndex}/{stepCount}, attackId={attackInstanceId}, " +
                $"pending={hasPendingAttack}, pendingSkill={pendingSkillName}, pendingBase={pendingBaseName}, " +
                $"pendingStep={pendingComboStepIndex}/{pendingComboStepCount}, currentStep={currentComboStepIndex}/{currentComboStepCount}",
                Logg.LoggingMode.Completed);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void LogPendingExecuteState(string stage, SkillTypeSO skill, int attackInstanceId, Health target)
        {
            string ownerName = gameObject != null ? gameObject.name : "null";
            int ownerInstanceId = gameObject != null ? gameObject.GetInstanceID() : 0;
            string skillName = skill.IsNotNull() ? skill.name : "null";
            string targetName = target.IsNotNull() ? target.name : "null";

            Logg.Log(
                $"[{nameof(SkillController)}.{nameof(TryExecutePendingAttack)}] " +
                $"owner={ownerName}#{ownerInstanceId}, stage={stage}, frame={Time.frameCount}, time={Time.time:0.000}, " +
                $"skill={skillName}, attackId={attackInstanceId}, target={targetName}",
                Logg.LoggingMode.Completed);
        }

        // 소비 없는 프리뷰 공격 소스 생성
        public bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;
            if (!HasActiveSkill || attacker.IsNull()) return false;

            var previewSkill = GetPreviewSkill();
            if (previewSkill.IsNull()) return false;

            return TryBuildAttackSource(attacker, previewSkill, 0, out attackSource);
        }

        // 보류 공격 즉시 실행 시도
        public bool TryExecutePendingAttack(IAttacker attacker, Health target)
        {
            if (!hasPendingAttack) return false;

            // 공격자/대상 무효 입력 가드
            if (attacker.IsNull() || target.IsNull())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            // 보류 생성 주체 불일치 또는 상태 불일치 가드
            if (!ReferenceEquals(pendingAttacker, attacker) || !IsPendingAttackReusableState())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            var baseSkill = pendingBaseSkill.IsNotNull()
                ? pendingBaseSkill
                : (HasActiveSkill ? skillBook.ActiveSkill : null);
            if (baseSkill.IsNull())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            float timingScale = ResolveSkillTimingScale(attacker);
            if (!skillCaster.IsReady(baseSkill))
            {
                LogPendingExecuteState("execute_blocked_not_ready", pendingAttackSkill, 0, target);
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            float scaledCooldown = baseSkill.Cooldown / Mathf.Max(0.01f, timingScale);
            if (!skillCaster.Consume(baseSkill, scaledCooldown))
            {
                LogPendingExecuteState("execute_consume_failed", pendingAttackSkill, 0, target);
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            int attackInstanceId = TakeNextAttackInstanceId();
            CommitComboProgress(baseSkill, pendingComboStepIndex, pendingComboStepCount);
            ScheduleActiveComboTimeout(baseSkill, pendingComboStepCount);
            SkillTypeSO executedSkill = pendingAttackSkill;
            executingSkill = executedSkill;

            if (attacker is Component attackerComponent && executedSkill.IsNotNull())
            {
                var effectContext = new SkillEffectPlayContext(
                    attackerComponent,
                    null,
                    default,
                    hasHitPoint: false,
                    attackInstanceId);
                SkillEffectPlayer.TryPlaySkillEffect(executedSkill, SkillEffectTrigger.OnConsume, effectContext);
            }

            OnSkillConsumed?.Invoke(executedSkill);

            LogPendingExecuteState("execute_begin", executedSkill, attackInstanceId, target);
            bool executed = ExecutePendingAttack(target, timingScale, attackInstanceId);
            if (executed)
            {
                LogPendingExecuteState("execute_success", executedSkill, attackInstanceId, target);
                lastUsedSkill = executedSkill;
                executingSkill = null;
                // 성공 실행 시 보류 상태 해제
                ClearPendingAttack();
                UpdateResolvedSkillFromPreview(forceNotify: true);
                return true;
            }

            LogPendingExecuteState("execute_rejected", executedSkill, attackInstanceId, target);
            // 실행 거부 시 보류 상태 정리
            CancelPendingAttack(PendingCancelReason.ExecutionRejected, refreshResolvedFromPreview: true);
            return false;
        }

        // 보류 공격 실제 실행 경로
        private bool ExecutePendingAttack(Health target, float timingScale, int attackInstanceId)
        {
            var skill = pendingAttackSkill;
            if (skill.IsNull())
            {
                return false;
            }

            if (!skill.HasSubSkills)
            {
                return TryExecuteSingleSkill(skill, target, timingScale, attackInstanceId);
            }

            bool anyExecuted = false;
            var subSkills = skill.SubSkills;
            for (int i = 0; i < subSkills.Count; i++)
            {
                var subSkill = subSkills[i];
                if (subSkill.IsNull())
                {
                    continue;
                }

                if (TryExecuteSingleSkill(subSkill, target, timingScale, attackInstanceId))
                {
                    anyExecuted = true;
                }
            }

            return anyExecuted;
        }

        private bool TryExecuteSingleSkill(SkillTypeSO skill, Health target, float timingScale, int attackInstanceId)
        {
            if (skill.IsNull())
            {
                return false;
            }

            if (!TryBuildAttackSource(pendingAttacker, skill, attackInstanceId, out var attackSource))
            {
                return false;
            }

            var context = new SkillExecutionContext(pendingAttacker, target, skill, attackSource, timingScale);
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            if (skill.ExecutionProfile is { HasActions: true } executionProfile)
            {
                StartCoroutine(executionProfile.Execute(context, this));
                return true;
            }

            if (skill.HasProjectile)
            {
                if (projectileExecutor.IsNotNull() &&
                    projectileExecutor.TryExecuteProjectile(attackSource, target, skill))
                {
                    return true;
                }

                Logg.LogWarning($"[{gameObject.name}.{nameof(SkillController)}] Projectile executor missing. Falling back to direct hit.");
            }

            combatSystem ??= ServiceLocator.Get<ICombatSystem>();
            if (combatSystem == null)
            {
                return false;
            }

            combatSystem.ApplyHit(attackSource.ToRequest(target));
            return true;
        }

        // 보류 공격 상태 적재
        private void SetPendingAttack(IAttacker attacker, SkillTypeSO skill, SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            pendingAttacker = attacker;
            pendingAttackSkill = skill;
            pendingBaseSkill = baseSkill;
            pendingComboStepIndex = Mathf.Max(0, stepIndex);
            pendingComboStepCount = Mathf.Max(1, stepCount);
            pendingAttackIssuedFrame = Time.frameCount;
            hasPendingAttack = true;
        }

        // 보류 공격 상태 초기화
        private void ClearPendingAttack()
        {
            hasPendingAttack = false;
            pendingAttackSkill = null;
            pendingAttacker = null;
            pendingBaseSkill = null;
            pendingComboStepIndex = 0;
            pendingComboStepCount = 1;
            pendingAttackIssuedFrame = -1;
        }

        // 현재 캐시 값 기준 보류 재사용 상태 평가
        private bool IsPendingAttackReusableState()
        {
            return IsPendingAttackReusableStateFor(pendingBaseSkill, pendingComboStepIndex, pendingComboStepCount);
        }

        // 지정 단계 기준 보류 재사용 상태 평가
        private bool IsPendingAttackReusableStateFor(SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            // 기본 필드 무결성 가드
            if (!hasPendingAttack || pendingAttackSkill.IsNull())
            {
                return false;
            }

            // 활성 스킬 불일치 가드
            if (baseSkill.IsNull() || !HasActiveSkill || skillBook.ActiveSkill != baseSkill)
            {
                return false;
            }

            // 비콤보 스킬 재사용 조건
            if (!baseSkill.HasComboSteps)
            {
                return stepIndex == 0 && stepCount <= 1 && pendingAttackSkill == baseSkill;
            }

            int resolvedStepCount = Mathf.Max(1, baseSkill.ComboStepCount);
            int resolvedStepIndex = Mathf.Clamp(stepIndex, 0, resolvedStepCount - 1);
            if (resolvedStepCount != Mathf.Max(1, stepCount))
            {
                return false;
            }

            // 단계별 해석 스킬 일치 검증
            return baseSkill.GetComboStepSkill(resolvedStepIndex, baseSkill) == pendingAttackSkill;
        }

        // 보류 공격 취소 + 필요 시 해석 상태 갱신
        private void CancelPendingAttack(PendingCancelReason reason, bool refreshResolvedFromPreview)
        {
            _ = reason;

            if (!hasPendingAttack)
            {
                return;
            }

            ClearPendingAttack();
            executingSkill = null;

            if (refreshResolvedFromPreview)
            {
                UpdateResolvedSkillFromPreview(forceNotify: true);
            }
        }
    }
}

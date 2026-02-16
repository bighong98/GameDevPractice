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
                if (TryReusePendingAttack(attacker, out attackSource))
                {
                    executingSkill = pendingAttackSkill;
                    LogConsumeState("reuse_pending_success");
                    SyncDebugValues();
                    return true;
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

            int attackInstanceId = TakeNextAttackInstanceId();
            // 공격 소스 생성 실패 가드
            if (!TryBuildAttackSource(attacker, resolved, attackInstanceId, out attackSource))
            {
                LogConsumeState("build_attack_source_failed", baseSkill, resolved, stepIndex, stepCount, attackInstanceId);
                return false;
            }

            // 타이밍 스케일 반영 쿨다운 계산
            float cooldownTimingScale = ResolveSkillTimingScale(attacker);
            float scaledCooldown = baseSkill.Cooldown / Mathf.Max(0.01f, cooldownTimingScale);
            if (!skillCaster.Consume(baseSkill, scaledCooldown))
            {
                LogConsumeState("consume_failed", baseSkill, resolved, stepIndex, stepCount, attackInstanceId);
                attackSource = default;
                return false;
            }

            // 콤보 진행/타임아웃/보류 상태 커밋
            CommitComboProgress(baseSkill, stepIndex, stepCount);
            ScheduleActiveComboTimeout(baseSkill, stepCount);
            SetPendingAttack(attacker, resolved, attackSource, baseSkill, stepIndex, stepCount);
            executingSkill = resolved;
            LogConsumeState("pending_set_before_resolved_update", baseSkill, resolved, stepIndex, stepCount, attackInstanceId);
            UpdateResolvedSkillFromPreview(forceNotify: true);
            LogConsumeState("after_resolved_update", baseSkill, resolved, stepIndex, stepCount, attackInstanceId);

            // 스킬 이펙트 재생 경로
            if (attacker is Component attackerComponent)
            {
                SkillEffectPlayer.TryPlaySkillEffect(resolved, attackerComponent.transform);
            }

            OnSkillConsumed?.Invoke(resolved);
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

            Logg.Log(
                $"[{nameof(SkillController)}.{nameof(TryConsumeActiveSkill)}] " +
                $"stage={stage}, frame={Time.frameCount}, time={Time.time:0.000}, " +
                $"active={activeSkillName}, resolvedCurrent={resolvedSkillName}, " +
                $"baseArg={baseSkillName}, resolvedArg={resolvedPreviewName}, step={stepIndex}/{stepCount}, attackId={attackInstanceId}, " +
                $"pending={hasPendingAttack}, pendingSkill={pendingSkillName}, pendingBase={pendingBaseName}, " +
                $"pendingStep={pendingComboStepIndex}/{pendingComboStepCount}, currentStep={currentComboStepIndex}/{currentComboStepCount}",
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

            bool executed = ExecutePendingAttack(target);
            if (executed)
            {
                // 성공 실행 시 보류 상태 해제
                ClearPendingAttack();
                UpdateResolvedSkillFromPreview(forceNotify: true);
                return true;
            }

            // 실행 거부 시 보류 상태 정리
            CancelPendingAttack(PendingCancelReason.ExecutionRejected, refreshResolvedFromPreview: true);
            return false;
        }

        // 보류 공격 실제 실행 경로
        private bool ExecutePendingAttack(Health target)
        {
            var skill = pendingAttackSkill;
            if (skill.IsNull())
            {
                return false;
            }

            float timingScale = ResolveSkillTimingScale(pendingAttacker);
            int attackInstanceId = pendingAttackSource.AttackInstanceId;

            if (!skill.HasSubSkills)
            {
                return TryExecuteSingleSkill(skill, target, timingScale, attackInstanceId, usePendingAttackSource: true);
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

                if (TryExecuteSingleSkill(subSkill, target, timingScale, attackInstanceId, usePendingAttackSource: false))
                {
                    anyExecuted = true;
                }
            }

            return anyExecuted;
        }

        private bool TryExecuteSingleSkill(SkillTypeSO skill, Health target, float timingScale, int attackInstanceId, bool usePendingAttackSource)
        {
            if (skill.IsNull())
            {
                return false;
            }

            AttackSource attackSource = default;
            if (usePendingAttackSource)
            {
                attackSource = pendingAttackSource;
            }
            else if (!TryBuildAttackSource(pendingAttacker, skill, attackInstanceId, out attackSource))
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
        private void SetPendingAttack(IAttacker attacker, SkillTypeSO skill, in AttackSource attackSource, SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            pendingAttacker = attacker;
            pendingAttackSkill = skill;
            pendingAttackSource = attackSource;
            pendingBaseSkill = baseSkill;
            pendingComboStepIndex = Mathf.Max(0, stepIndex);
            pendingComboStepCount = Mathf.Max(1, stepCount);
            hasPendingAttack = true;
        }

        // 보류 공격 상태 초기화
        private void ClearPendingAttack()
        {
            hasPendingAttack = false;
            pendingAttackSource = default;
            pendingAttackSkill = null;
            pendingAttacker = null;
            pendingBaseSkill = null;
            pendingComboStepIndex = 0;
            pendingComboStepCount = 1;
        }

        // 동일 공격자 기준 보류 공격 재사용 시도
        private bool TryReusePendingAttack(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;

            // 재사용 가능한 보류 상태 검증
            if (!TryGetValidPendingState(out var baseSkill, out var stepIndex, out var stepCount))
            {
                return false;
            }

            // 공격자 일치 검증
            if (attacker.IsNull() || !ReferenceEquals(pendingAttacker, attacker))
            {
                return false;
            }

            attackSource = pendingAttackSource;
            SetResolvedSkill(pendingAttackSkill, baseSkill, stepIndex, stepCount, forceNotify: true);
            return true;
        }

        // 유효 보류 상태 조회
        private bool TryGetValidPendingState(out SkillTypeSO baseSkill, out int stepIndex, out int stepCount)
        {
            baseSkill = null;
            stepIndex = 0;
            stepCount = 1;

            // 보류 스킬 미존재 가드
            if (!hasPendingAttack || pendingAttackSkill.IsNull())
            {
                return false;
            }

            baseSkill = pendingBaseSkill;
            stepIndex = Mathf.Max(0, pendingComboStepIndex);
            stepCount = Mathf.Max(1, pendingComboStepCount);

            return IsPendingAttackReusableStateFor(baseSkill, stepIndex, stepCount);
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
            if (!hasPendingAttack || pendingAttackSkill.IsNull() || pendingAttackSource.Skill.IsNull())
            {
                return false;
            }

            // 공격 소스 스킬 불일치 가드
            if (pendingAttackSource.Skill != pendingAttackSkill)
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

            // 콤보 타임아웃 초과 가드
            var context = GetOrCreateComboContext(baseSkill);
            float comboTimeout = Mathf.Max(0f, baseSkill.ComboTimeout);
            if (comboTimeout > 0f &&
                context.LastConsumeTime >= 0f &&
                Time.time > context.LastConsumeTime + comboTimeout)
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

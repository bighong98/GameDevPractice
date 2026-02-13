using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
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

            // 보류 공격 존재 시 재사용 우선 경로
            if (hasPendingAttack)
            {
                if (TryReusePendingAttack(attacker, out attackSource))
                {
                    SyncDebugValues();
                    return true;
                }

                // 재사용 실패 보류 상태 정리
                CancelPendingAttack(PendingCancelReason.InvalidatedOnConsume, refreshResolvedFromPreview: true);
            }

            var baseSkill = skillBook.ActiveSkill;
            // 쿨다운 미준비 가드
            if (!skillCaster.IsReady(baseSkill)) return false;
            // 현재 콤보 단계 기준 해석 실패 가드
            if (!TryResolveSkillPreview(baseSkill, out var resolved, out var stepIndex, out var stepCount)) return false;

            int attackInstanceId = TakeNextAttackInstanceId();
            // 공격 소스 생성 실패 가드
            if (!TryBuildAttackSource(attacker, resolved, attackInstanceId, out attackSource)) return false;

            // 타이밍 스케일 반영 쿨다운 계산
            float cooldownTimingScale = ResolveSkillTimingScale(attacker);
            float scaledCooldown = baseSkill.Cooldown / Mathf.Max(0.01f, cooldownTimingScale);
            if (!skillCaster.Consume(baseSkill, scaledCooldown))
            {
                attackSource = default;
                return false;
            }

            // 콤보 진행/타임아웃/보류 상태 커밋
            CommitComboProgress(baseSkill, stepIndex, stepCount);
            ScheduleActiveComboTimeout(baseSkill, stepCount);
            SetPendingAttack(attacker, resolved, attackSource, baseSkill, stepIndex, stepCount);
            UpdateResolvedSkillFromPreview(forceNotify: true);

            // 스킬 이펙트 재생 경로
            if (attacker is Component attackerComponent)
            {
                SkillEffectPlayer.TryPlaySkillEffect(resolved, attackerComponent.transform);
            }

            OnSkillConsumed?.Invoke(resolved);
            SyncDebugValues();
            return true;
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
            float timingScale = ResolveSkillTimingScale(pendingAttacker);
            var context = new SkillExecutionContext(pendingAttacker, target, skill, pendingAttackSource, timingScale);

            // 타게팅 정책 위반 가드
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            // 실행 프로파일 액션 우선 경로
            if (skill.IsNotNull() && skill.ExecutionProfile is { HasActions: true } executionProfile)
            {
                StartCoroutine(executionProfile.Execute(context, this));
                return true;
            }

            // 투사체 스킬 처리 경로
            if (skill.IsNotNull() && skill.HasProjectile)
            {
                if (projectileExecutor.IsNotNull() &&
                    projectileExecutor.TryExecuteProjectile(pendingAttackSource, target, skill))
                {
                    return true;
                }

                Logg.LogWarning($"[{gameObject.name}.{nameof(SkillController)}] Projectile executor missing. Falling back to direct hit.");
            }

            // 직접 타격 폴백 경로
            combatSystem ??= ServiceLocator.Get<ICombatSystem>();
            if (combatSystem == null)
            {
                return false;
            }

            combatSystem.ApplyHit(pendingAttackSource.ToRequest(target));
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
            if (baseSkill.ComboSequence is not { HasSteps: true } comboSequence)
            {
                return stepIndex == 0 && stepCount <= 1 && pendingAttackSkill == baseSkill;
            }

            int resolvedStepCount = Mathf.Max(1, comboSequence.StepCount);
            int resolvedStepIndex = Mathf.Clamp(stepIndex, 0, resolvedStepCount - 1);
            if (resolvedStepCount != Mathf.Max(1, stepCount))
            {
                return false;
            }

            // 콤보 타임아웃 초과 가드
            var context = GetOrCreateComboContext(baseSkill);
            float comboTimeout = Mathf.Max(0f, comboSequence.ComboTimeout);
            if (comboTimeout > 0f &&
                context.LastConsumeTime >= 0f &&
                Time.time > context.LastConsumeTime + comboTimeout)
            {
                return false;
            }

            // 단계별 해석 스킬 일치 검증
            return comboSequence.GetStepSkill(resolvedStepIndex, baseSkill) == pendingAttackSkill;
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

            if (refreshResolvedFromPreview)
            {
                UpdateResolvedSkillFromPreview(forceNotify: true);
            }
        }
    }
}

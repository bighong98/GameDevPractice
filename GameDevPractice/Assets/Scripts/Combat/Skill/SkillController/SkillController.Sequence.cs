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
    // 콤보 시퀀스 해석/진행/타임아웃 관리 파트
    public sealed partial class SkillController
    {
        // 활성 스킬 콤보 타임아웃 조회
        public bool TryGetActiveSequenceTimeout(SkillTypeSO skill, out float remainingTimeout, out float totalTimeout)
        {
            remainingTimeout = 0f;
            totalTimeout = 0f;

            // 활성 스킬 불일치 및 미초기화 가드
            if (skill.IsNull() || !HasActiveSkill || skillBook == null || skillBook.ActiveSkill != skill)
                return false;

            // 콤보 시퀀스 미보유 가드
            if (!skill.HasComboSteps)
                return false;

            int stepCount = Mathf.Max(1, skill.ComboStepCount);
            if (stepCount <= 1)
                return false;

            // 유효 타임아웃 미설정 가드
            float comboTimeout = Mathf.Max(0f, skill.ComboTimeout);
            if (comboTimeout <= 0f)
                return false;

            var context = GetOrCreateComboContext(skill);
            // 첫 소비 이전 또는 다음 단계 없음 가드
            if (context.LastConsumeTime < 0f || context.NextStepIndex <= 0)
                return false;

            // 타임아웃 초과 가드
            float elapsed = Time.time - context.LastConsumeTime;
            if (elapsed >= comboTimeout)
                return false;

            remainingTimeout = comboTimeout - elapsed;
            totalTimeout = comboTimeout;
            return true;
        }

        // 현재 공격 프리뷰 스킬 결정
        private SkillTypeSO GetPreviewSkill()
        {
            // 보류 공격 존재 시 현재 소비된 스킬 프리뷰를 우선 유지
            if (hasPendingAttack && pendingAttackSkill.IsNotNull())
            {
                return pendingAttackSkill;
            }

            if (!HasActiveSkill) return null;

            // 콤보 반영 해석 결과 우선 반환
            if (TryResolveSkillPreview(skillBook.ActiveSkill, out var previewSkill, out _, out _))
                return previewSkill;

            // 해석 실패 시 활성 스킬 폴백
            return skillBook.ActiveSkill;
        }

        // 베이스 스킬 기준 현재 콤보 단계 해석
        private bool TryResolveSkillPreview(SkillTypeSO baseSkill, out SkillTypeSO resolved, out int stepIndex, out int stepCount)
        {
            resolved = null;
            stepIndex = 0;
            stepCount = 1;

            if (baseSkill.IsNull()) return false;

            // 콤보 시퀀스 미보유 스킬 단일 단계 처리
            if (!baseSkill.HasComboSteps)
            {
                resolved = baseSkill;
                return true;
            }

            stepCount = Mathf.Max(1, baseSkill.ComboStepCount);
            var context = GetOrCreateComboContext(baseSkill);

            int nextStepIndex = context.NextStepIndex;
            // 타임아웃 초과 시 0단계 리셋
            if (ShouldResetCombo(baseSkill.ComboTimeout, context))
            {
                nextStepIndex = 0;
            }

            // 범위 외 단계 인덱스 보정
            if (nextStepIndex < 0 || nextStepIndex >= stepCount)
            {
                nextStepIndex = 0;
            }

            resolved = baseSkill.GetComboStepSkill(nextStepIndex, baseSkill);
            stepIndex = nextStepIndex;
            return true;
        }

        // 콤보 리셋 필요 여부 계산
        private static bool ShouldResetCombo(float timeout, ComboContext context)
        {
            if (context.LastConsumeTime < 0f) return true;
            if (timeout <= 0f) return true;

            return Time.time > context.LastConsumeTime + timeout;
        }

        // 스킬 소비 결과 기반 다음 콤보 단계 커밋
        private void CommitComboProgress(SkillTypeSO baseSkill, int consumedStepIndex, int stepCount)
        {
            var context = GetOrCreateComboContext(baseSkill);
            context.LastConsumeTime = Time.time;

            // 단일 단계 스킬 즉시 초기화
            if (stepCount <= 1)
            {
                context.NextStepIndex = 0;
                return;
            }

            // 마지막 단계 이후 0단계 순환
            int nextStep = consumedStepIndex + 1;
            context.NextStepIndex = nextStep < stepCount ? nextStep : 0;
        }

        // 활성 스킬 콤보 타임아웃 예약
        private void ScheduleActiveComboTimeout(SkillTypeSO baseSkill, int stepCount)
        {
            // 기존 예약 취소 후 재예약 흐름
            CancelActiveComboTimeoutRoutine();

            // 활성 스킬 불일치 가드
            if (!HasActiveSkill || baseSkill.IsNull() || skillBook.ActiveSkill != baseSkill)
                return;

            // 비콤보/단일단계 스킬 가드
            if (stepCount <= 1 || !baseSkill.HasComboSteps)
                return;

            var context = GetOrCreateComboContext(baseSkill);
            // 다음 단계 없음 상태 가드
            if (context.NextStepIndex <= 0)
                return;

            float comboTimeout = Mathf.Max(0f, baseSkill.ComboTimeout);
            isActiveComboTimeoutPending = true;
            activeComboTimeoutBaseSkill = baseSkill;
            activeComboTimeoutExpectedNextStepIndex = context.NextStepIndex;
            activeComboTimeoutAt = Time.time + comboTimeout;
        }

        // 활성 콤보 타임아웃 예약 상태 초기화
        private void CancelActiveComboTimeoutRoutine()
        {
            isActiveComboTimeoutPending = false;
            activeComboTimeoutBaseSkill = null;
            activeComboTimeoutExpectedNextStepIndex = 0;
            activeComboTimeoutAt = 0f;
        }

        // 특정 베이스 스킬 콤보 진행 상태 리셋
        private void ResetComboProgress(SkillTypeSO baseSkill)
        {
            if (baseSkill.IsNull()) return;

            var context = GetOrCreateComboContext(baseSkill);
            context.NextStepIndex = 0;
            context.LastConsumeTime = -1f;
        }

        // 베이스 스킬별 콤보 컨텍스트 조회/생성
        private ComboContext GetOrCreateComboContext(SkillTypeSO baseSkill)
        {
            if (!comboContexts.TryGetValue(baseSkill, out var context))
            {
                context = new ComboContext();
                comboContexts[baseSkill] = context;
            }

            return context;
        }

        // 프리뷰 기준 해석 스킬 상태 동기화
        private void UpdateResolvedSkillFromPreview(bool forceNotify)
        {
            // 보류 공격 존재 시 유효성 재평가보다 현재 소비된 스킬 상태를 우선 반영
            if (hasPendingAttack && pendingAttackSkill.IsNotNull())
            {
                var resolvedBaseSkill = pendingBaseSkill.IsNotNull()
                    ? pendingBaseSkill
                    : (HasActiveSkill ? skillBook.ActiveSkill : null);

                SetResolvedSkill(
                    pendingAttackSkill,
                    resolvedBaseSkill,
                    Mathf.Max(0, pendingComboStepIndex),
                    Mathf.Max(1, pendingComboStepCount),
                    forceNotify);
                return;
            }

            // 활성 스킬 미보유 시 해석 상태 초기화
            if (!HasActiveSkill)
            {
                SetResolvedSkill(null, null, 0, 1, forceNotify);
                return;
            }

            // 프리뷰 해석 실패 시 활성 스킬 폴백
            if (!TryResolveSkillPreview(skillBook.ActiveSkill, out var preview, out var stepIndex, out var stepCount))
            {
                SetResolvedSkill(skillBook.ActiveSkill, skillBook.ActiveSkill, 0, 1, forceNotify);
                return;
            }

            SetResolvedSkill(preview, skillBook.ActiveSkill, stepIndex, stepCount, forceNotify);
        }

        // 해석 스킬/콤보 단계 상태 저장 + 이벤트 발행
        private void SetResolvedSkill(SkillTypeSO skill, SkillTypeSO baseSkill, int stepIndex, int stepCount, bool forceNotify)
        {
            bool skillChanged = resolvedSkill != skill;
            bool comboChanged = resolvedBaseSkill != baseSkill ||
                               currentComboStepIndex != stepIndex ||
                               currentComboStepCount != stepCount;

            resolvedSkill = skill;
            resolvedBaseSkill = baseSkill;
            currentComboStepIndex = Mathf.Max(0, stepIndex);
            currentComboStepCount = Mathf.Max(1, stepCount);

            // 해석 스킬 변경 이벤트 발행 조건
            if (forceNotify || skillChanged)
            {
                OnResolvedSkillChanged?.Invoke(resolvedSkill);
            }

            // 콤보 단계 변경 이벤트 발행 조건
            if (forceNotify || comboChanged)
            {
                OnComboStepChanged?.Invoke(resolvedBaseSkill, currentComboStepIndex, currentComboStepCount);
            }
        }
    }
}

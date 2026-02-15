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
    // 스킬 등록/활성/쿨다운 조회 책임 파트
    public sealed partial class SkillController
    {
        // 스킬 등록 + 쿨다운 추적기 연동 처리
        public bool RegisterSkill(SkillTypeSO skill, bool setActive = false)
        {
            // null 입력 및 초기화 이전 가드
            if (skill.IsNull() || skillBook == null || skillCaster == null) return false;

            // 스킬북 등록 시도 + 쿨다운 추적 등록
            bool added = skillBook.Register(skill);
            skillCaster.TrackSkill(skill);

            // 신규 등록 성공 시 목록 변경 이벤트 발행
            if (added)
            {
                OnSkillBookChanged?.Invoke();
                SyncAvailableSkillSet(forceNotify: true);
            }

            // 요청 시 즉시 활성 스킬 전환
            if (setActive)
            {
                SetActiveSkill(skill);
            }

            return added;
        }

        // 활성 스킬 전환 + 콤보/보류 상태 초기화 처리
        public bool SetActiveSkill(SkillTypeSO skill)
        {
            // null 입력 및 초기화 이전 가드
            if (skill.IsNull() || skillBook == null) return false;

            // 미등록 스킬 입력 시 선등록 경로
            if (!skillBook.Contains(skill))
            {
                RegisterSkill(skill);
            }

            bool changed = skillBook.SetActive(skill);
            if (changed)
            {
                // 이전 활성 스킬 파생 상태 정리
                CancelActiveComboTimeoutRoutine();
                ResetComboProgress(skill);
                ClearPendingAttack();
                OnActiveSkillChanged?.Invoke(skill);
            }

            // 프리뷰 기반 해석 결과 재동기화
            UpdateResolvedSkillFromPreview(forceNotify: changed || !HasResolvedSkill);
            SyncDebugValues();
            return changed;
        }

        // 사용 가능 스킬 변경 적용 처리
        public bool ApplySkillAvailabilityChange(SkillTypeSO targetSkill, SkillTypeSO replacementSkill = null)
        {
            if (targetSkill.IsNull() || skillBook == null) return false;

            // 교체 대상 미지정 시 슬롯 하이라이트만 요청
            if (replacementSkill.IsNull() || replacementSkill == targetSkill)
            {
                OnSkillSlotHighlightRequested?.Invoke(targetSkill);
                return true;
            }

            if (!skillBook.Contains(replacementSkill))
            {
                RegisterSkill(replacementSkill);
            }

            bool replaced = skillBook.ReplaceAvailableSkill(targetSkill, replacementSkill);
            if (!replaced)
            {
                OnSkillSlotHighlightRequested?.Invoke(targetSkill);
                return false;
            }

            if (HasActiveSkill && ActiveSkill == targetSkill)
            {
                SetActiveSkill(replacementSkill);
            }

            SyncAvailableSkillSet(forceNotify: true);
            return true;
        }

        // 스킬 잔여 쿨다운 조회
        public float GetRemainingCooldown(SkillTypeSO skill)
        {
            if (skill.IsNull() || skillCaster == null) return 0f;
            return skillCaster.GetRemainingCooldown(skill);
        }

        // 투사체 실행기 등록
        public void SetProjectileExecutor(ISkillProjectileExecutor executor)
        {
            projectileExecutor = executor;
        }

        // 동일 인스턴스 실행기 해제
        public void ClearProjectileExecutor(ISkillProjectileExecutor executor)
        {
            if (projectileExecutor == executor)
            {
                projectileExecutor = null;
            }
        }

        // 무기 기본 스킬 정책 반영 + 사용 가능 스킬 목록 동기화
        private void SyncAvailableSkillSet(bool forceNotify)
        {
            if (skillBook == null) return;

            bool changed = false;
            if (syncWithEquippedWeapon && equippedWeaponDefaultSkill.IsNotNull())
            {
                changed |= skillBook.RemoveAvailableWhere(skill =>
                    skill.IsNotNull() &&
                    skill.SkillCategory == SkillCategory.WeaponDefaultSkill &&
                    skill != equippedWeaponDefaultSkill);

                changed |= skillBook.SetAvailable(equippedWeaponDefaultSkill, true);
            }

            EnsureActiveSkillIsAvailable();

            if (forceNotify || changed)
            {
                OnAvailableSkillsChanged?.Invoke();
            }
        }

        // 활성 스킬이 사용 가능 목록에서 빠진 경우 첫 사용 가능 스킬로 보정
        private void EnsureActiveSkillIsAvailable()
        {
            if (skillBook == null || !HasActiveSkill) return;
            if (skillBook.ContainsAvailable(skillBook.ActiveSkill)) return;

            if (skillBook.TryGetFirstAvailable(out var firstAvailableSkill))
            {
                SetActiveSkill(firstAvailableSkill);
            }
        }
    }
}

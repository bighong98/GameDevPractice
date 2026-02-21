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
        private const string unarmedFallbackWeaponAddressKey = "Unarmed.asset";

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
                executingSkill = null;
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

        // 무기 스킬 정책 반영 + 사용 가능 스킬 목록 동기화
        private void SyncAvailableSkillSet(bool forceNotify, SkillTypeSO preferredActiveSkill = null)
        {
            if (skillBook == null) return;

            bool changed = false;
            if (syncWithEquippedWeapon)
            {
                equippedWeaponSkillByCategory.Clear();
                for (int i = 0; i < equippedWeaponSkills.Count; i++)
                {
                    SkillTypeSO weaponSkill = equippedWeaponSkills[i];
                    if (weaponSkill.IsNull())
                    {
                        continue;
                    }

                    // 동일 카테고리에서는 뒤 항목 우선
                    equippedWeaponSkillByCategory[weaponSkill.SkillCategory] = weaponSkill;
                }

                if (equippedWeaponSkillByCategory.Count > 0)
                {
                    changed |= skillBook.RemoveAvailableWhere(skill =>
                        skill.IsNotNull() &&
                        equippedWeaponSkillByCategory.TryGetValue(skill.SkillCategory, out var selectedWeaponSkill) &&
                        skill != selectedWeaponSkill);
                }

                foreach (var pair in equippedWeaponSkillByCategory)
                {
                    changed |= skillBook.SetAvailable(pair.Value, true);
                }
            }

            changed |= EnsureActiveSkillIsAvailable(preferredActiveSkill);
            bool slotOrderChanged = RebuildOrderedAvailableSkills(notifySlotChanges: true);

            if (forceNotify || changed || slotOrderChanged)
            {
                OnAvailableSkillsChanged?.Invoke();
            }
        }

        // 활성 스킬 보정 (기존 활성 유지 -> BasicSkill 우선 -> 캐릭터 기본 무기 BasicSkill fallback)
        private bool EnsureActiveSkillIsAvailable(SkillTypeSO preferredActiveSkill)
        {
            if (skillBook == null) return false;

            if (preferredActiveSkill.IsNotNull() && skillBook.ContainsAvailable(preferredActiveSkill))
            {
                SetActiveSkill(preferredActiveSkill);
                return false;
            }

            if (HasActiveSkill && skillBook.ContainsAvailable(skillBook.ActiveSkill)) return false;

            if (TryGetAvailableSkillByCategory(SkillCategory.BasicSkill, out var availableBasicSkill))
            {
                SetActiveSkill(availableBasicSkill);
                return false;
            }

            bool changed = false;
            if (TryResolveDefaultWeaponBasicSkill(out var fallbackBasicSkill))
            {
                if (!skillBook.Contains(fallbackBasicSkill))
                {
                    bool added = skillBook.Register(fallbackBasicSkill);
                    skillCaster?.TrackSkill(fallbackBasicSkill);
                    if (added)
                    {
                        OnSkillBookChanged?.Invoke();
                        changed = true;
                    }
                }

                changed |= skillBook.SetAvailable(fallbackBasicSkill, true);
                SetActiveSkill(fallbackBasicSkill);
                return changed;
            }

            if (skillBook.TryGetFirstAvailable(out var firstAvailableSkill))
            {
                SetActiveSkill(firstAvailableSkill);
            }

            return changed;
        }

        private bool TryGetAvailableSkillByCategory(SkillCategory category, out SkillTypeSO skill)
        {
            skill = null;
            if (skillBook == null)
            {
                return false;
            }

            var availableSkills = skillBook.AvailableSkills;
            for (int i = availableSkills.Count - 1; i >= 0; i--)
            {
                SkillTypeSO candidate = availableSkills[i];
                if (candidate.IsNull() || candidate.SkillCategory != category)
                {
                    continue;
                }

                skill = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveDefaultWeaponBasicSkill(out SkillTypeSO basicSkill)
        {
            basicSkill = null;
            if (equipHolder.IsNotNull() && TryGetBasicSkillFromWeapon(equipHolder.DefaultWeaponInfo, out basicSkill))
            {
                return true;
            }

            resourceLoader ??= ServiceLocator.Get<IResourceLoader>();
            if (resourceLoader.IsNull() ||
                !resourceLoader.TryLoad(unarmedFallbackWeaponAddressKey, out WeaponTypeSO unarmedWeapon) ||
                unarmedWeapon.IsNull())
            {
                return false;
            }

            return TryGetBasicSkillFromWeapon(unarmedWeapon, out basicSkill);
        }

        private static bool TryGetBasicSkillFromWeapon(WeaponTypeSO weapon, out SkillTypeSO basicSkill)
        {
            basicSkill = null;
            if (weapon.IsNull())
            {
                return false;
            }

            IReadOnlyList<SkillTypeSO> weaponSkills = weapon.DefaultSkills;
            for (int i = weaponSkills.Count - 1; i >= 0; i--)
            {
                SkillTypeSO candidate = weaponSkills[i];
                if (candidate.IsNull() || candidate.SkillCategory != SkillCategory.BasicSkill)
                {
                    continue;
                }

                basicSkill = candidate;
                return true;
            }

            return false;
        }
    }
}

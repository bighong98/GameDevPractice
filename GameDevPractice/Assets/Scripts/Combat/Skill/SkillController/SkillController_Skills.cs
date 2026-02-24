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
        private const string legacySkillProviderId = "legacy.manual";
        private const string equippedWeaponSkillProviderId = "equipment.weapon.current";
        private const int defaultLegacyProviderPriority = 100;
        private const int defaultEquippedWeaponProviderPriority = 300;

        // 스킬 등록 + 쿨다운 추적기 연동 처리
        public bool RegisterSkill(SkillTypeSO skill, bool setActive = false)
        {
            // null 입력 및 초기화 이전 가드
            if (skill.IsNull() || skillBook == null || skillCaster == null) return false;

            if (skillBook.Contains(skill))
            {
                if (setActive)
                {
                    SetActiveSkill(skill);
                }

                return false;
            }

            // 스킬북 등록 시도 + 쿨다운 추적 등록
            bool added = skillBook.Register(skill, legacySkillProviderId, isAvailable: true);
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

        // provider 기반 스킬 등록 처리
        public bool RegisterSkillFromProvider(
            SkillTypeSO skill,
            string providerId,
            bool setActive = false,
            bool setAvailable = true)
        {
            if (skill.IsNull() || skillBook == null || skillCaster == null) return false;

            bool wasRegistered = skillBook.Contains(skill);
            bool changed = skillBook.Register(skill, providerId, setAvailable);
            skillCaster.TrackSkill(skill);

            bool isRegistered = skillBook.Contains(skill);
            if (!wasRegistered && isRegistered)
            {
                OnSkillBookChanged?.Invoke();
            }

            if (changed)
            {
                SyncAvailableSkillSet(forceNotify: true);
            }

            if (setActive)
            {
                SetActiveSkill(skill);
            }

            return changed;
        }

        // provider 기반 스킬 해제 처리
        public bool RemoveSkillFromProvider(SkillTypeSO skill, string providerId)
        {
            if (skill.IsNull() || skillBook == null) return false;

            bool wasRegistered = skillBook.Contains(skill);
            bool changed = skillBook.Revoke(skill, providerId);
            if (!changed)
            {
                return false;
            }

            bool isRegistered = skillBook.Contains(skill);
            if (wasRegistered && !isRegistered)
            {
                OnSkillBookChanged?.Invoke();
            }

            SyncAvailableSkillSet(forceNotify: true);
            return true;
        }

        public void SetSkillProviderPriority(string providerId, int priority)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return;
            }

            string resolvedProviderId = providerId.Trim();
            if (providerPriorityMap.TryGetValue(resolvedProviderId, out int currentPriority) &&
                currentPriority == priority)
            {
                return;
            }

            providerPriorityMap[resolvedProviderId] = priority;
            SyncAvailableSkillSet(forceNotify: true);
        }

        private void InitializeProviderPriorityMap()
        {
            providerPriorityMap.Clear();
            providerPriorityMap[equippedWeaponSkillProviderId] = defaultEquippedWeaponProviderPriority;
            providerPriorityMap[legacySkillProviderId] = defaultLegacyProviderPriority;
        }

        private int ResolveProviderPriority(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return 0;
            }

            string resolvedProviderId = providerId.Trim();
            return providerPriorityMap.TryGetValue(resolvedProviderId, out int priority) ? priority : 0;
        }

        private int ResolveBestAvailableProviderPriority(SkillTypeSO skill)
        {
            if (skillBook == null || skill.IsNull())
            {
                return int.MinValue;
            }

            return skillBook.GetMaxAvailableProviderPriority(skill, ResolveProviderPriority);
        }

        private bool RebuildEffectiveAvailableSkills()
        {
            if (skillBook == null)
            {
                bool hadAny = effectiveAvailableSkills.Count > 0;
                effectiveAvailableSkills.Clear();
                effectiveAvailableSkillSet.Clear();
                categoryWinnerByType.Clear();
                categoryWinnerRawIndexByType.Clear();
                categoryWinnerProviderPriorityByType.Clear();
                return hadAny;
            }

            var previous = new List<SkillTypeSO>(effectiveAvailableSkills);
            effectiveAvailableSkills.Clear();
            effectiveAvailableSkillSet.Clear();
            categoryWinnerByType.Clear();
            categoryWinnerRawIndexByType.Clear();
            categoryWinnerProviderPriorityByType.Clear();

            var rawAvailableSkills = skillBook.AvailableSkills;
            for (int i = 0; i < rawAvailableSkills.Count; i++)
            {
                SkillTypeSO candidateSkill = rawAvailableSkills[i];
                if (candidateSkill.IsNull())
                {
                    continue;
                }

                SkillCategory category = candidateSkill.SkillCategory;
                int candidateProviderPriority = ResolveBestAvailableProviderPriority(candidateSkill);

                if (!categoryWinnerByType.TryGetValue(category, out SkillTypeSO currentWinner))
                {
                    categoryWinnerByType[category] = candidateSkill;
                    categoryWinnerRawIndexByType[category] = i;
                    categoryWinnerProviderPriorityByType[category] = candidateProviderPriority;
                    continue;
                }

                int currentWinnerRawIndex = categoryWinnerRawIndexByType[category];
                int currentWinnerProviderPriority = categoryWinnerProviderPriorityByType[category];
                if (ShouldReplaceCategoryWinner(
                        currentWinner,
                        currentWinnerProviderPriority,
                        currentWinnerRawIndex,
                        candidateSkill,
                        candidateProviderPriority,
                        i))
                {
                    categoryWinnerByType[category] = candidateSkill;
                    categoryWinnerRawIndexByType[category] = i;
                    categoryWinnerProviderPriorityByType[category] = candidateProviderPriority;
                }
            }

            for (int i = 0; i < rawAvailableSkills.Count; i++)
            {
                SkillTypeSO skill = rawAvailableSkills[i];
                if (skill.IsNull())
                {
                    continue;
                }

                if (!categoryWinnerByType.TryGetValue(skill.SkillCategory, out SkillTypeSO winner) ||
                    winner != skill ||
                    !effectiveAvailableSkillSet.Add(skill))
                {
                    continue;
                }

                effectiveAvailableSkills.Add(skill);
            }

            if (previous.Count != effectiveAvailableSkills.Count)
            {
                return true;
            }

            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i] != effectiveAvailableSkills[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldReplaceCategoryWinner(
            SkillTypeSO currentWinner,
            int currentWinnerProviderPriority,
            int currentWinnerRawIndex,
            SkillTypeSO candidateSkill,
            int candidateProviderPriority,
            int candidateRawIndex)
        {
            if (candidateProviderPriority != currentWinnerProviderPriority)
            {
                return candidateProviderPriority > currentWinnerProviderPriority;
            }

            if (candidateRawIndex != currentWinnerRawIndex)
            {
                return candidateRawIndex > currentWinnerRawIndex;
            }

            return string.CompareOrdinal(
                       candidateSkill.IsNotNull() ? candidateSkill.name : string.Empty,
                       currentWinner.IsNotNull() ? currentWinner.name : string.Empty) > 0;
        }

        private bool ContainsEffectiveAvailableSkill(SkillTypeSO skill)
        {
            return skill.IsNotNull() && effectiveAvailableSkillSet.Contains(skill);
        }

        // 활성 스킬 전환 + 콤보/보류 상태 초기화 처리
        public bool SetActiveSkill(SkillTypeSO skill)
        {
            // null 입력은 활성 스킬 해제 요청으로 처리
            if (skillBook == null) return false;
            if (skill.IsNull())
            {
                return ClearActiveSkill();
            }

            // 미등록 스킬 입력 시 선등록 경로
            if (!skillBook.Contains(skill))
            {
                RegisterSkill(skill);
            }

            bool preserveComboProgress = ShouldPreserveComboProgressOnActivation(skill);
            bool changed = skillBook.SetActive(skill);
            if (changed)
            {
                // 이전 활성 스킬 파생 상태 정리
                CancelActiveComboTimeoutRoutine();
                if (!preserveComboProgress)
                {
                    ResetComboProgress(skill);
                }

                ClearPendingAttack();
                executingSkill = null;
                OnActiveSkillChanged?.Invoke(skill);
            }

            // 프리뷰 기반 해석 결과 재동기화
            UpdateResolvedSkillFromPreview(forceNotify: changed || !HasResolvedSkill);
            SyncDebugValues();
            return changed;
        }

        public bool TryClearActiveSkill(bool respectComboPreserveMarker = false)
        {
            return ClearActiveSkill(
                resetComboProgress: true,
                preserveComboProgressOnNextActivation: false,
                respectComboPreserveMarker: respectComboPreserveMarker);
        }

        // 필요 시점에만 활성 스킬을 지연 선택/보정
        public bool TryRequestActiveSkill()
        {
            if (skillBook == null) return false;
            RebuildEffectiveAvailableSkills();

            if (HasActiveSkill && ContainsEffectiveAvailableSkill(skillBook.ActiveSkill))
            {
                return true;
            }

            bool notifySkillBookChanged = false;
            bool forceNotifyAvailable = false;

            SkillTypeSO requestedSkill = null;
            if (defaultActiveSkill.IsNotNull() && ContainsEffectiveAvailableSkill(defaultActiveSkill))
            {
                requestedSkill = defaultActiveSkill;
            }
            else if (TryGetAvailableSkillByCategory(SkillCategory.BasicSkill, out var availableBasicSkill))
            {
                requestedSkill = availableBasicSkill;
            }
            else if (TryResolveDefaultWeaponBasicSkill(out var fallbackBasicSkill))
            {
                if (!skillBook.Contains(fallbackBasicSkill))
                {
                    bool added = skillBook.Register(fallbackBasicSkill);
                    skillCaster?.TrackSkill(fallbackBasicSkill);
                    if (added)
                    {
                        notifySkillBookChanged = true;
                        forceNotifyAvailable = true;
                    }
                }

                if (skillBook.SetAvailable(fallbackBasicSkill, true))
                {
                    forceNotifyAvailable = true;
                }

                requestedSkill = fallbackBasicSkill;
            }
            else if (effectiveAvailableSkills.Count > 0 && effectiveAvailableSkills[0].IsNotNull())
            {
                requestedSkill = effectiveAvailableSkills[0];
            }

            if (notifySkillBookChanged)
            {
                OnSkillBookChanged?.Invoke();
            }

            if (forceNotifyAvailable)
            {
                SyncAvailableSkillSet(forceNotify: true);
            }

            if (requestedSkill.IsNull())
            {
                ClearActiveSkill();
                return false;
            }

            SetActiveSkill(requestedSkill);
            return HasActiveSkill && skillBook.ActiveSkill == requestedSkill;
        }

        private bool ClearActiveSkill()
        {
            return ClearActiveSkill(
                resetComboProgress: true,
                preserveComboProgressOnNextActivation: false,
                respectComboPreserveMarker: false);
        }

        private bool ClearActiveSkill(
            bool resetComboProgress,
            bool preserveComboProgressOnNextActivation,
            bool respectComboPreserveMarker)
        {
            if (skillBook == null || !HasActiveSkill)
            {
                return false;
            }

            SkillTypeSO previousActiveSkill = skillBook.ActiveSkill;
            if (respectComboPreserveMarker &&
                ShouldIgnoreExternalClearByComboPreserveMarker(previousActiveSkill))
            {
                return false;
            }

            bool changed = skillBook.ClearActive();
            if (!changed)
            {
                return false;
            }

            CancelActiveComboTimeoutRoutine();
            if (resetComboProgress)
            {
                ResetComboProgress(previousActiveSkill, ignoreComboPreserveMarker: !respectComboPreserveMarker);
            }

            ClearPendingAttack();
            executingSkill = null;

            if (preserveComboProgressOnNextActivation && !resetComboProgress && previousActiveSkill.IsNotNull())
            {
                MarkPreservedComboProgressSkill(previousActiveSkill);
            }
            else
            {
                ClearPreservedComboProgressSkill(previousActiveSkill);
                if (!resetComboProgress)
                {
                    ClearExternalClearIgnoreComboMarkerSkill(previousActiveSkill);
                }
            }

            OnActiveSkillChanged?.Invoke(null);
            UpdateResolvedSkillFromPreview(forceNotify: true);
            SyncDebugValues();
            return true;
        }

        private bool ShouldPreserveComboProgressOnActivation(SkillTypeSO skill)
        {
            if (skill.IsNull())
            {
                ClearPreservedComboProgressSkill();
                return false;
            }

            if (preservedComboProgressSkill.IsNull())
            {
                return false;
            }

            bool shouldPreserve = preservedComboProgressSkill == skill;
            ClearPreservedComboProgressSkill(skill);
            return shouldPreserve;
        }

        private void MarkPreservedComboProgressSkill(SkillTypeSO skill)
        {
            preservedComboProgressSkill = skill;
        }

        private void ClearPreservedComboProgressSkill(SkillTypeSO skill = null)
        {
            if (skill.IsNull() || preservedComboProgressSkill == skill)
            {
                preservedComboProgressSkill = null;
            }
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
        private void SyncAvailableSkillSet(bool forceNotify)
        {
            if (skillBook == null) return;

            bool changed = false;
            bool effectiveChanged = RebuildEffectiveAvailableSkills();
            changed |= EnsureActiveSkillIsValid();
            bool slotOrderChanged = RebuildOrderedAvailableSkills(notifySlotChanges: true);

            if (forceNotify || effectiveChanged || changed || slotOrderChanged)
            {
                OnAvailableSkillsChanged?.Invoke();
            }
        }

        // 현재 활성 스킬이 사용 가능 목록 밖으로 밀려난 경우 해제 처리
        private bool EnsureActiveSkillIsValid()
        {
            if (skillBook == null) return false;
            if (!HasActiveSkill) return false;
            if (ContainsEffectiveAvailableSkill(skillBook.ActiveSkill)) return false;

            return ClearActiveSkill();
        }

        private bool TryGetAvailableSkillByCategory(SkillCategory category, out SkillTypeSO skill)
        {
            skill = null;
            if (skillBook == null)
            {
                return false;
            }

            var availableSkills = effectiveAvailableSkills;
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

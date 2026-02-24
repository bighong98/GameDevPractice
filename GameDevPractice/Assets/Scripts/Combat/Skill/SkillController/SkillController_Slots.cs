using System;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    // 슬롯 노출 순서 계산 및 변경 알림 파트
    public sealed partial class SkillController
    {
        // 카테고리 정렬 프로파일 로드 주소 키
        private const string categorySortProfileAddressKey = "SkillCategorySortProfileSO";

        // 슬롯 정렬 상태 초기화 진입점
        private void InitializeSlotOrderingState()
        {
            RebuildCategoryPriorityMap();
            TryLoadCategorySortProfile();
        }

        // 슬롯 인덱스 기준 정렬 스킬 조회
        public bool TryGetOrderedSkillAt(int slotIndex, out SkillTypeSO skill)
        {
            skill = null;

            if (slotIndex < 0 || slotIndex >= orderedAvailableSkills.Count)
            {
                return false;
            }

            skill = orderedAvailableSkills[slotIndex];
            return skill.IsNotNull();
        }

        // 지정 스킬 슬롯 인덱스 역탐색
        public int FindOrderedSkillSlotIndex(SkillTypeSO skill)
        {
            if (skill.IsNull())
            {
                return -1;
            }

            for (int i = 0; i < orderedAvailableSkills.Count; i++)
            {
                if (orderedAvailableSkills[i] == skill)
                {
                    return i;
                }
            }

            return -1;
        }

        // 프리로드 완료 시 슬롯 정렬 상태 재초기화
        private void HandleResourceLabelLoaded(string label)
        {
            if (!string.Equals(label, Constants.PreLoadLabel, StringComparison.Ordinal))
            {
                return;
            }

            InitializeSlotOrderingState();
        }

        // 카테고리 정렬 프로파일 로드 및 반영
        private void TryLoadCategorySortProfile()
        {
            if (resourceLoader == null || string.IsNullOrWhiteSpace(categorySortProfileAddressKey))
            {
                return;
            }

            if (!resourceLoader.TryLoad(categorySortProfileAddressKey, out SkillCategorySortProfileSO loadedProfile) ||
                loadedProfile.IsNull())
            {
                return;
            }

            if (ReferenceEquals(loadedProfile, categorySortProfile))
            {
                return;
            }

            categorySortProfile = loadedProfile;
            RebuildCategoryPriorityMap();

            bool changed = RebuildOrderedAvailableSkills(notifySlotChanges: true);
            if (changed)
            {
                OnAvailableSkillsChanged?.Invoke();
            }
        }

        // 최종 사용 가능 스킬 슬롯 정렬 목록 재구축
        private bool RebuildOrderedAvailableSkills(bool notifySlotChanges)
        {
            orderedAvailableSkills.Clear();
            registeredSkillOrder.Clear();
            uniqueSkillBuffer.Clear();

            if (skillBook == null)
            {
                return false;
            }

            var registeredSkills = skillBook.Skills;
            for (int i = 0; i < registeredSkills.Count; i++)
            {
                SkillTypeSO skill = registeredSkills[i];
                if (skill.IsNull() || registeredSkillOrder.ContainsKey(skill))
                {
                    continue;
                }

                registeredSkillOrder.Add(skill, i);
            }

            var availableSkills = effectiveAvailableSkills;
            for (int i = 0; i < availableSkills.Count; i++)
            {
                SkillTypeSO skill = availableSkills[i];
                if (skill.IsNull() || !uniqueSkillBuffer.Add(skill))
                {
                    continue;
                }

                orderedAvailableSkills.Add(skill);
            }

            orderedAvailableSkills.Sort(CompareSkillsForSlotOrder);

            bool changed = false;
            int slotCount = Mathf.Max(orderedAvailableSkills.Count, orderedAvailableSkillsSnapshot.Count);
            for (int i = 0; i < slotCount; i++)
            {
                SkillTypeSO prevSkill = i < orderedAvailableSkillsSnapshot.Count ? orderedAvailableSkillsSnapshot[i] : null;
                SkillTypeSO currSkill = i < orderedAvailableSkills.Count ? orderedAvailableSkills[i] : null;

                if (prevSkill == currSkill)
                {
                    continue;
                }

                changed = true;
                if (notifySlotChanges)
                {
                    OnSkillSlotChanged?.Invoke(i, currSkill);
                }
            }

            orderedAvailableSkillsSnapshot.Clear();
            orderedAvailableSkillsSnapshot.AddRange(orderedAvailableSkills);
            return changed;
        }

        // 슬롯 정렬 비교 규칙 적용
        private int CompareSkillsForSlotOrder(SkillTypeSO left, SkillTypeSO right)
        {
            int leftCategoryPriority = GetCategoryPriority(left);
            int rightCategoryPriority = GetCategoryPriority(right);
            if (leftCategoryPriority != rightCategoryPriority)
            {
                return rightCategoryPriority.CompareTo(leftCategoryPriority);
            }

            int leftRegisteredOrder = ResolveRegisteredOrder(left);
            int rightRegisteredOrder = ResolveRegisteredOrder(right);
            if (leftRegisteredOrder != rightRegisteredOrder)
            {
                return leftRegisteredOrder.CompareTo(rightRegisteredOrder);
            }

            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }

        // 스킬 카테고리 우선순위 조회
        private int GetCategoryPriority(SkillTypeSO skill)
        {
            SkillCategory category = skill.IsNotNull() ? skill.SkillCategory : SkillCategory.AdditiveSkill;
            if (categoryPriorityMap.TryGetValue(category, out int priority))
            {
                return priority;
            }

            return ResolveDefaultCategoryPriority(category);
        }

        // 등록 순서 인덱스 조회
        private int ResolveRegisteredOrder(SkillTypeSO skill)
        {
            if (skill.IsNull())
            {
                return int.MaxValue;
            }

            return registeredSkillOrder.TryGetValue(skill, out int index) ? index : int.MaxValue;
        }

        // 카테고리 우선순위 맵 재구축
        private void RebuildCategoryPriorityMap()
        {
            categoryPriorityMap.Clear();
            if (categorySortProfile != null && categorySortProfile.Rules != null)
            {
                for (int i = 0; i < categorySortProfile.Rules.Count; i++)
                {
                    SkillCategorySortProfileSO.Rule rule = categorySortProfile.Rules[i];
                    categoryPriorityMap[rule.category] = rule.priority;
                }
            }

            EnsureCategoryPriority(SkillCategory.BasicSkill);
            EnsureCategoryPriority(SkillCategory.AdditiveSkill);
            EnsureCategoryPriority(SkillCategory.UltimateSkill);
        }

        // 누락 카테고리 기본 우선순위 보정
        private void EnsureCategoryPriority(SkillCategory category)
        {
            if (!categoryPriorityMap.ContainsKey(category))
            {
                categoryPriorityMap[category] = ResolveDefaultCategoryPriority(category);
            }
        }

        // 카테고리 기본 우선순위 계산
        private static int ResolveDefaultCategoryPriority(SkillCategory category)
        {
            return category switch
            {
                SkillCategory.BasicSkill => 300,
                SkillCategory.AdditiveSkill => 200,
                SkillCategory.UltimateSkill => 100,
                _ => 0
            };
        }
    }
}

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
    // 내부 상태 저장 타입 모음 파트
    public sealed partial class SkillController
    {
        // 보류 공격 취소 사유 분류
        private enum PendingCancelReason
        {
            InvalidatedOnConsume,
            InvalidatedByStateChange,
            InvalidatedOnExecute,
            ExecutionRejected,
            StalePendingDetected
        }

        // 등록 스킬 목록/활성 스킬 저장소
        private sealed class SkillBook
        {
            // 등록 스킬 내부 목록
            private readonly List<SkillTypeSO> skills = new();
            // 사용 가능 스킬 내부 목록
            private readonly List<SkillTypeSO> availableSkills = new();

            // 등록 스킬 읽기 전용 목록
            public IReadOnlyList<SkillTypeSO> Skills => skills;
            // 사용 가능 스킬 읽기 전용 목록
            public IReadOnlyList<SkillTypeSO> AvailableSkills => availableSkills;
            // 현재 활성 스킬 참조
            public SkillTypeSO ActiveSkill { get; private set; }

            // 스킬 등록 처리
            public bool Register(SkillTypeSO skill)
            {
                if (skill.IsNull() || Contains(skill)) return false;

                skills.Add(skill);
                availableSkills.Add(skill);
                // 최초 등록 스킬 자동 활성화
                if (ActiveSkill.IsNull())
                {
                    ActiveSkill = skill;
                }

                return true;
            }

            // 스킬 등록 여부 조회
            public bool Contains(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return skills.Contains(skill);
            }

            // 활성 스킬 교체 처리
            public bool SetActive(SkillTypeSO skill)
            {
                if (!Contains(skill)) return false;
                if (ActiveSkill == skill) return false;

                ActiveSkill = skill;
                return true;
            }

            // 사용 가능 스킬 포함 여부 조회
            public bool ContainsAvailable(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return availableSkills.Contains(skill);
            }

            // 사용 가능 스킬 노출 여부 갱신
            public bool SetAvailable(SkillTypeSO skill, bool isAvailable)
            {
                if (skill.IsNull() || !Contains(skill)) return false;

                bool contains = availableSkills.Contains(skill);
                if (isAvailable && !contains)
                {
                    availableSkills.Add(skill);
                    return true;
                }

                if (!isAvailable && contains)
                {
                    availableSkills.Remove(skill);
                    return true;
                }

                return false;
            }

            // 사용 가능 스킬 치환 처리
            public bool ReplaceAvailableSkill(SkillTypeSO targetSkill, SkillTypeSO replacementSkill)
            {
                if (targetSkill.IsNull() || replacementSkill.IsNull()) return false;
                if (!Contains(targetSkill) || !Contains(replacementSkill)) return false;
                if (targetSkill == replacementSkill) return false;

                int targetIndex = availableSkills.IndexOf(targetSkill);
                if (targetIndex < 0) return false;

                availableSkills[targetIndex] = replacementSkill;

                // 치환 이후 중복 항목 정리
                for (int i = availableSkills.Count - 1; i >= 0; i--)
                {
                    if (i != targetIndex && availableSkills[i] == replacementSkill)
                    {
                        availableSkills.RemoveAt(i);
                        if (i < targetIndex)
                        {
                            targetIndex--;
                        }
                    }
                }

                return true;
            }

            // 조건 기반 사용 가능 스킬 제거 처리
            public bool RemoveAvailableWhere(Predicate<SkillTypeSO> predicate)
            {
                if (predicate == null || availableSkills.Count == 0) return false;
                return availableSkills.RemoveAll(predicate) > 0;
            }

            // 첫 번째 등록 스킬 조회
            public bool TryGetFirst(out SkillTypeSO firstSkill)
            {
                if (skills.Count > 0 && skills[0].IsNotNull())
                {
                    firstSkill = skills[0];
                    return true;
                }

                firstSkill = null;
                return false;
            }

            // 첫 번째 사용 가능 스킬 조회
            public bool TryGetFirstAvailable(out SkillTypeSO firstSkill)
            {
                if (availableSkills.Count > 0 && availableSkills[0].IsNotNull())
                {
                    firstSkill = availableSkills[0];
                    return true;
                }

                firstSkill = null;
                return false;
            }
        }

        // 스킬 쿨다운 준비 상태 캐시
        private sealed class SkillCaster
        {
            // 스킬별 다음 준비 완료 시각
            private readonly Dictionary<SkillTypeSO, float> nextReadyAt = new();
            // 직전 프레임 준비 상태 캐시
            private readonly Dictionary<SkillTypeSO, bool> cachedReadyState = new();

            // 스킬 추적 등록
            public void TrackSkill(SkillTypeSO skill)
            {
                if (skill.IsNull()) return;

                if (!nextReadyAt.ContainsKey(skill))
                {
                    nextReadyAt[skill] = 0f;
                }

                cachedReadyState[skill] = true;
            }

            // 현재 준비 상태 조회
            public bool IsReady(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return true;

                return Time.time >= readyTime;
            }

            // 스킬 소비 + 다음 준비 시각 기록
            public bool Consume(SkillTypeSO skill, float cooldown)
            {
                if (skill.IsNull() || !IsReady(skill)) return false;

                float appliedCooldown = Mathf.Max(0f, cooldown);
                nextReadyAt[skill] = appliedCooldown > 0f ? Time.time + appliedCooldown : Time.time;
                cachedReadyState[skill] = appliedCooldown <= 0f;
                return true;
            }

            // 잔여 쿨다운 조회
            public float GetRemainingCooldown(SkillTypeSO skill)
            {
                if (skill.IsNull()) return 0f;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return 0f;

                return Mathf.Max(0f, readyTime - Time.time);
            }

            // 준비 상태 전이 감지 + 콜백 발행
            public void PollReady(IReadOnlyList<SkillTypeSO> skills, Action<SkillTypeSO> onReady)
            {
                if (skills == null) return;

                for (int i = 0; i < skills.Count; i++)
                {
                    var skill = skills[i];
                    if (skill.IsNull()) continue;

                    bool wasReady = cachedReadyState.TryGetValue(skill, out var prev) && prev;
                    bool isReady = IsReady(skill);
                    cachedReadyState[skill] = isReady;

                    if (!wasReady && isReady)
                    {
                        onReady?.Invoke(skill);
                    }
                }
            }
        }

        // 콤보 단계 진행 컨텍스트
        private sealed class ComboContext
        {
            // 다음 소비 시도 단계 인덱스
            public int NextStepIndex;
            // 마지막 소비 시각
            public float LastConsumeTime = -1f;
        }
    }
}

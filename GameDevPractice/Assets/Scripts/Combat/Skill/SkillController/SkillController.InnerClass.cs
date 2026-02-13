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
            ExecutionRejected
        }

        // 등록 스킬 목록/활성 스킬 저장소
        private sealed class SkillBook
        {
            // 등록 스킬 내부 목록
            private readonly List<SkillTypeSO> skills = new();

            // 등록 스킬 읽기 전용 목록
            public IReadOnlyList<SkillTypeSO> Skills => skills;
            // 현재 활성 스킬 참조
            public SkillTypeSO ActiveSkill { get; private set; }

            // 스킬 등록 처리
            public bool Register(SkillTypeSO skill)
            {
                if (skill.IsNull() || Contains(skill)) return false;

                skills.Add(skill);
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

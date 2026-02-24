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
            private const string LegacyProviderId = "legacy.manual";

            // 등록 스킬 내부 목록
            private readonly List<SkillTypeSO> skills = new();
            // 사용 가능 스킬 내부 목록
            private readonly List<SkillTypeSO> availableSkills = new();
            // 스킬별 부여 provider 집합
            private readonly Dictionary<SkillTypeSO, HashSet<string>> providersBySkill = new();
            // 스킬별 사용 가능 provider 집합
            private readonly Dictionary<SkillTypeSO, HashSet<string>> availableProvidersBySkill = new();
            // provider별 부여 스킬 집합
            private readonly Dictionary<string, HashSet<SkillTypeSO>> skillsByProvider = new(StringComparer.Ordinal);
            // provider별 사용 가능 스킬 집합
            private readonly Dictionary<string, HashSet<SkillTypeSO>> availableSkillsByProvider = new(StringComparer.Ordinal);

            // 등록 스킬 읽기 전용 목록
            public IReadOnlyList<SkillTypeSO> Skills => skills;
            // 사용 가능 스킬 읽기 전용 목록
            public IReadOnlyList<SkillTypeSO> AvailableSkills => availableSkills;
            // 현재 활성 스킬 참조
            public SkillTypeSO ActiveSkill { get; private set; }

            // 스킬 등록 처리
            public bool Register(SkillTypeSO skill)
            {
                return Register(skill, LegacyProviderId, isAvailable: true);
            }

            // provider 기반 스킬 등록 처리
            public bool Register(SkillTypeSO skill, string providerId, bool isAvailable)
            {
                if (skill.IsNull()) return false;

                string resolvedProviderId = ResolveProviderId(providerId);
                bool changed = false;

                if (!providersBySkill.TryGetValue(skill, out var providers))
                {
                    providers = new HashSet<string>(StringComparer.Ordinal);
                    providersBySkill.Add(skill, providers);
                }

                if (providers.Add(resolvedProviderId))
                {
                    if (!skillsByProvider.TryGetValue(resolvedProviderId, out var providerSkills))
                    {
                        providerSkills = new HashSet<SkillTypeSO>();
                        skillsByProvider.Add(resolvedProviderId, providerSkills);
                    }

                    providerSkills.Add(skill);
                    if (!skills.Contains(skill))
                    {
                        skills.Add(skill);
                    }

                    changed = true;
                }

                if (isAvailable)
                {
                    changed |= SetAvailable(skill, resolvedProviderId, true);
                }
                else
                {
                    changed |= SetAvailable(skill, resolvedProviderId, false);
                }

                return changed;
            }

            // 스킬 등록 여부 조회
            public bool Contains(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return providersBySkill.TryGetValue(skill, out var providers) && providers.Count > 0;
            }

            // 활성 스킬 교체 처리
            public bool SetActive(SkillTypeSO skill)
            {
                if (!Contains(skill)) return false;
                if (ActiveSkill == skill) return false;

                ActiveSkill = skill;
                return true;
            }

            public bool ClearActive()
            {
                if (ActiveSkill.IsNull()) return false;

                ActiveSkill = null;
                return true;
            }

            // 사용 가능 스킬 포함 여부 조회
            public bool ContainsAvailable(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return availableProvidersBySkill.TryGetValue(skill, out var providers) && providers.Count > 0;
            }

            // 사용 가능 provider 기준 최고 우선순위 조회
            public int GetMaxAvailableProviderPriority(SkillTypeSO skill, Func<string, int> resolveProviderPriority)
            {
                if (skill.IsNull() || resolveProviderPriority == null)
                {
                    return int.MinValue;
                }

                if (!availableProvidersBySkill.TryGetValue(skill, out var providers) || providers == null || providers.Count == 0)
                {
                    return int.MinValue;
                }

                int maxPriority = int.MinValue;
                foreach (string providerId in providers)
                {
                    int priority = resolveProviderPriority(providerId);
                    if (priority > maxPriority)
                    {
                        maxPriority = priority;
                    }
                }

                return maxPriority;
            }

            // 사용 가능 스킬 노출 여부 갱신
            public bool SetAvailable(SkillTypeSO skill, bool isAvailable)
            {
                return SetAvailable(skill, LegacyProviderId, isAvailable);
            }

            // provider 기반 사용 가능 스킬 노출 여부 갱신
            public bool SetAvailable(SkillTypeSO skill, string providerId, bool isAvailable)
            {
                if (skill.IsNull()) return false;

                string resolvedProviderId = ResolveProviderId(providerId);
                if (!providersBySkill.TryGetValue(skill, out var registeredProviders) ||
                    !registeredProviders.Contains(resolvedProviderId))
                {
                    return false;
                }

                if (!availableProvidersBySkill.TryGetValue(skill, out var availableProviders))
                {
                    availableProviders = new HashSet<string>(StringComparer.Ordinal);
                    availableProvidersBySkill.Add(skill, availableProviders);
                }

                bool wasAvailable = availableProviders.Count > 0;
                bool changed = false;
                if (isAvailable)
                {
                    if (availableProviders.Add(resolvedProviderId))
                    {
                        if (!availableSkillsByProvider.TryGetValue(resolvedProviderId, out var providerAvailableSkills))
                        {
                            providerAvailableSkills = new HashSet<SkillTypeSO>();
                            availableSkillsByProvider.Add(resolvedProviderId, providerAvailableSkills);
                        }

                        providerAvailableSkills.Add(skill);
                        changed = true;
                    }
                }
                else
                {
                    if (availableProviders.Remove(resolvedProviderId))
                    {
                        if (availableSkillsByProvider.TryGetValue(resolvedProviderId, out var providerAvailableSkills))
                        {
                            providerAvailableSkills.Remove(skill);
                            if (providerAvailableSkills.Count == 0)
                            {
                                availableSkillsByProvider.Remove(resolvedProviderId);
                            }
                        }

                        changed = true;
                    }
                }

                bool isNowAvailable = availableProviders.Count > 0;
                if (!wasAvailable && isNowAvailable)
                {
                    if (!availableSkills.Contains(skill))
                    {
                        availableSkills.Add(skill);
                    }

                    changed = true;
                }
                else if (wasAvailable && !isNowAvailable)
                {
                    changed |= availableSkills.Remove(skill);
                }

                if (!isNowAvailable)
                {
                    availableProvidersBySkill.Remove(skill);
                }

                return changed;
            }

            // 사용 가능 스킬 치환 처리
            public bool ReplaceAvailableSkill(SkillTypeSO targetSkill, SkillTypeSO replacementSkill)
            {
                if (targetSkill.IsNull() || replacementSkill.IsNull()) return false;
                if (!Contains(targetSkill) || !Contains(replacementSkill)) return false;
                if (targetSkill == replacementSkill) return false;

                bool changed = false;
                changed |= SetAvailable(targetSkill, LegacyProviderId, false);
                changed |= SetAvailable(replacementSkill, LegacyProviderId, true);
                return changed;
            }

            // 조건 기반 사용 가능 스킬 제거 처리
            public bool RemoveAvailableWhere(Predicate<SkillTypeSO> predicate)
            {
                if (predicate == null || availableSkills.Count == 0) return false;

                var matchedSkills = new List<SkillTypeSO>();
                for (int i = 0; i < availableSkills.Count; i++)
                {
                    SkillTypeSO skill = availableSkills[i];
                    if (skill.IsNull() || !predicate(skill))
                    {
                        continue;
                    }

                    matchedSkills.Add(skill);
                }

                bool changed = false;
                for (int i = 0; i < matchedSkills.Count; i++)
                {
                    SkillTypeSO skill = matchedSkills[i];
                    if (!availableProvidersBySkill.TryGetValue(skill, out var providerSet) || providerSet.Count == 0)
                    {
                        continue;
                    }

                    var providers = new List<string>(providerSet);
                    for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
                    {
                        changed |= SetAvailable(skill, providers[providerIndex], false);
                    }
                }

                return changed;
            }

            // provider 기반 스킬 부여 해제 처리
            public bool Revoke(SkillTypeSO skill, string providerId)
            {
                if (skill.IsNull()) return false;

                string resolvedProviderId = ResolveProviderId(providerId);
                if (!providersBySkill.TryGetValue(skill, out var providers) ||
                    !providers.Contains(resolvedProviderId))
                {
                    return false;
                }

                bool changed = true;
                changed |= SetAvailable(skill, resolvedProviderId, false);
                providers.Remove(resolvedProviderId);

                if (skillsByProvider.TryGetValue(resolvedProviderId, out var providerSkills))
                {
                    providerSkills.Remove(skill);
                    if (providerSkills.Count == 0)
                    {
                        skillsByProvider.Remove(resolvedProviderId);
                    }
                }

                if (providers.Count == 0)
                {
                    providersBySkill.Remove(skill);
                    availableProvidersBySkill.Remove(skill);
                    availableSkills.Remove(skill);
                    skills.Remove(skill);
                }

                return changed;
            }

            // provider 단위 스킬 집합 동기화 처리
            public bool ReplaceProviderSkills(string providerId, IReadOnlyList<SkillTypeSO> providerSkills, bool isAvailable)
            {
                string resolvedProviderId = ResolveProviderId(providerId);

                var nextSkills = new HashSet<SkillTypeSO>();
                if (providerSkills != null)
                {
                    for (int i = 0; i < providerSkills.Count; i++)
                    {
                        SkillTypeSO skill = providerSkills[i];
                        if (skill.IsNull())
                        {
                            continue;
                        }

                        nextSkills.Add(skill);
                    }
                }

                bool changed = false;

                if (skillsByProvider.TryGetValue(resolvedProviderId, out var currentProviderSkills) &&
                    currentProviderSkills.Count > 0)
                {
                    var removeTargets = new List<SkillTypeSO>();
                    foreach (var currentSkill in currentProviderSkills)
                    {
                        if (currentSkill.IsNull() || !nextSkills.Contains(currentSkill))
                        {
                            removeTargets.Add(currentSkill);
                        }
                    }

                    for (int i = 0; i < removeTargets.Count; i++)
                    {
                        changed |= Revoke(removeTargets[i], resolvedProviderId);
                    }
                }

                foreach (var nextSkill in nextSkills)
                {
                    changed |= Register(nextSkill, resolvedProviderId, isAvailable);
                    changed |= SetAvailable(nextSkill, resolvedProviderId, isAvailable);
                }

                return changed;
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

            // 빈 provider 식별자 레거시 키 정규화
            private static string ResolveProviderId(string providerId)
            {
                return string.IsNullOrWhiteSpace(providerId) ? LegacyProviderId : providerId.Trim();
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

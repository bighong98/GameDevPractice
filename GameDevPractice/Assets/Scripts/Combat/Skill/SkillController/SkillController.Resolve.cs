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
    // 공격 소스 해석/생성 책임 파트
    public sealed partial class SkillController
    {
        // 공격자 타이밍 스케일 조회
        private static float ResolveSkillTimingScale(IAttacker attacker)
        {
            if (attacker is Component component &&
                component.TryGetComponent<ISkillTimingScaleProvider>(out var provider))
            {
                return Mathf.Max(0.01f, provider.SkillTimingScale);
            }

            return 1f;
        }

        // 스킬 데이터 기반 공격 소스 생성
        private bool TryBuildAttackSource(IAttacker attacker, SkillTypeSO skill, int attackInstanceId, out AttackSource attackSource)
        {
            attackSource = default;

            // 입력 유효성 가드
            if (skill.IsNull() || attacker.IsNull()) return false;

            // 공격 원천 스탯 유무 확인
            bool hasAttackSourceStat = TryResolveAttackSourceStat(skill, out var attackSourceStat);
            float sourceDamage = hasAttackSourceStat ? attackSourceStat.Value : skill.BaseDamage;
            float perHitDamage = Mathf.Max(0f, sourceDamage * skill.AttackCoefficient);
            int hitCount = Mathf.Max(1, skill.HitCount);

            // 단일 히트 스킬 생성 경로
            if (hitCount <= 1)
            {
                // 공격 계수 1 + 원천 스탯 사용 시 스탯 참조 직접 전달
                if (hasAttackSourceStat && Mathf.Approximately(skill.AttackCoefficient, 1f))
                {
                    attackSource = new AttackSource(
                        attacker,
                        attackSourceStat,
                        0f,
                        skill.DamageType,
                        attackInstanceId,
                        null,
                        skill);
                    return true;
                }

                // 단일 히트 고정 데미지 전달
                attackSource = new AttackSource(
                    attacker,
                    null,
                    perHitDamage,
                    skill.DamageType,
                    attackInstanceId,
                    null,
                    skill);
                return true;
            }

            // 다중 히트 데미지 배열 생성 경로
            var hitDamages = BuildHitDamages(perHitDamage, hitCount);
            attackSource = new AttackSource(attacker, null, perHitDamage, skill.DamageType, attackInstanceId, hitDamages, skill);
            return true;
        }

        // 전역 공격 인스턴스 ID 발급
        private static int TakeNextAttackInstanceId()
        {
            int next = Interlocked.Increment(ref attackSequence);
            if (next > 0)
                return next;

            // 오버플로우 이후 1 재시작 경로
            Interlocked.CompareExchange(ref attackSequence, 1, next);
            return 1;
        }

        // 스킬 지정 공격 원천 스탯 조회
        private bool TryResolveAttackSourceStat(SkillTypeSO skill, out IGameStat attackSourceStat)
        {
            attackSourceStat = null;
            if (skill.IsNull()) return false;

            if (statHolder.IsNotNull() &&
                skill.AttackSourceStatSO.IsNotNull() &&
                statHolder.TryGetStat(skill.AttackSourceStatSO, out var resolvedAttackSourceStat))
            {
                attackSourceStat = resolvedAttackSourceStat;
                return true;
            }

            return false;
        }

        // 히트 수 기반 데미지 배열 생성
        private List<float> BuildHitDamages(float perHitDamage, int hitCount)
        {
            int resolvedHitCount = Mathf.Max(1, hitCount);
            var hitDamages = primaryPendingHitDamages;

            hitDamages.Clear();
            if (hitDamages.Capacity < resolvedHitCount)
                hitDamages.Capacity = resolvedHitCount;

            for (int i = 0; i < resolvedHitCount; i++)
            {
                hitDamages.Add(perHitDamage);
            }

            return hitDamages;
        }
    }
}

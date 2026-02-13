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
    // 스킬 실행 서비스 구현 파트
    public sealed partial class SkillController
    {
        #region ISkillExecutionServices

        // 지정 대상 직접 타격 적용
        public bool TryApplyHit(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
        {
            // 타게팅 정책 위반 가드
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            // 데미지/히트 수 보정 공격 소스 생성
            var attackSource = BuildModifiedAttackSource(context.AttackSource, damageScale, hitCountOverride, allowReusableList: true);
            return TryApplyHitWithSource(attackSource, target);
        }

        // 지정 대상 투사체 발사 시도
        public bool TryLaunchProjectile(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
        {
            // 타게팅 정책 위반 가드
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            bool canExecuteProjectile = projectileExecutor.IsNotNull();
            // 투사체 사용 시 버퍼 충돌 방지 경로 선택
            var attackSource = BuildModifiedAttackSource(context.AttackSource, damageScale, hitCountOverride,
                allowReusableList: !canExecuteProjectile);

            if (canExecuteProjectile && projectileExecutor.TryExecuteProjectile(attackSource, target, context.Skill))
            {
                return true;
            }

            // 투사체 실패 시 직접 타격 폴백
            return TryApplyHitWithSource(attackSource, target);
        }

        // 반경 내 대상 검색
        public IReadOnlyList<Health> FindTargetsInRadius(
            SkillExecutionContext context,
            Vector3 center,
            float radius,
            int maxTargets,
            Health primaryTarget,
            bool includePrimary)
        {
            areaTargetsBuffer.Clear();

            // 반경 또는 수집 개수 무효 가드
            if (radius <= 0f || maxTargets <= 0)
            {
                return areaTargetsBuffer;
            }

            // 타게팅 정책 기반 레이어 마스크 조회
            int layerMask = ResolveTargetLayerMask(context);
            if (layerMask == 0)
            {
                return areaTargetsBuffer;
            }

            // 물리 오버랩 버퍼 확보 + 검색 수행
            EnsureOverlapBufferSize(maxTargets);
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                overlapBuffer,
                layerMask,
                QueryTriggerInteraction.Ignore);

            // 주대상 포함 옵션 처리
            if (includePrimary && primaryTarget.IsNotNull() &&
                Vector3.Distance(center, primaryTarget.transform.position) <= radius &&
                CanTargetWithPolicy(context, primaryTarget))
            {
                areaTargetsBuffer.Add(primaryTarget);
            }

            // 오버랩 결과 스캔 + 중복/정책 필터링
            int scanCount = Mathf.Min(hitCount, overlapBuffer.Length);
            for (int i = 0; i < scanCount && areaTargetsBuffer.Count < maxTargets; i++)
            {
                var collider = overlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (!collider.TryGetComponent<Health>(out var health))
                {
                    continue;
                }

                if (!includePrimary && health == primaryTarget)
                {
                    continue;
                }

                if (areaTargetsBuffer.Contains(health))
                {
                    continue;
                }

                if (!CanTargetWithPolicy(context, health))
                {
                    continue;
                }

                areaTargetsBuffer.Add(health);
            }

            return areaTargetsBuffer;
        }

        #endregion

        // 타게팅 평가기 지연 생성/재사용
        private SkillTargetingEvaluator GetTargetingEvaluator()
        {
            if (targetingEvaluator != null)
            {
                return targetingEvaluator;
            }

            targetingEvaluator = new SkillTargetingEvaluator(new SkillTargetLayerMaskResolver(skillTargetLayerMap));
            return targetingEvaluator;
        }

        // 타게팅 정책 기반 대상 유효성 평가
        private bool CanTargetWithPolicy(in SkillExecutionContext context, Health target)
        {
            if (target.IsNull())
            {
                return false;
            }

            // 공격자 또는 스킬 미확정 상태 폴백 규칙
            if (context.Attacker.IsNull() || context.Skill.IsNull())
            {
                return !target.IsDead;
            }

            return GetTargetingEvaluator().CanTarget(context, target);
        }

        // 타게팅 정책 기반 레이어 마스크 계산
        private int ResolveTargetLayerMask(in SkillExecutionContext context)
        {
            if (context.Attacker.IsNull() || context.Skill.IsNull())
            {
                return 0;
            }

            return GetTargetingEvaluator().ResolveTargetLayerMask(context);
        }

        // 공격 소스 직접 타격 적용
        private bool TryApplyHitWithSource(in AttackSource attackSource, Health target)
        {
            combatSystem ??= ServiceLocator.Get<ICombatSystem>();
            if (combatSystem == null)
            {
                return false;
            }

            combatSystem.ApplyHit(attackSource.ToRequest(target));
            return true;
        }

        // 물리 오버랩 버퍼 크기 보장
        private void EnsureOverlapBufferSize(int requiredSize)
        {
            if (requiredSize <= overlapBuffer.Length)
            {
                return;
            }

            int resized = Mathf.NextPowerOfTwo(requiredSize);
            overlapBuffer = new Collider[Mathf.Max(32, resized)];
        }

        // 데미지 스케일/히트 수 보정 공격 소스 생성
        private AttackSource BuildModifiedAttackSource(
            in AttackSource source,
            float damageScale,
            int hitCountOverride,
            bool allowReusableList)
        {
            float resolvedDamageScale = Mathf.Max(0f, damageScale);
            int resolvedHitCount = Mathf.Max(0, hitCountOverride);
            float sourceBaseDamage = source.AttackSourceStat?.Value ?? source.BaseDamage;

            // 기존 히트 분할 보유 소스 보정 경로
            if (source.HitDamages != null && source.HitDamages.Count > 0)
            {
                bool keepSourceHitDamages = Mathf.Approximately(resolvedDamageScale, 1f) &&
                                            (resolvedHitCount <= 0 || resolvedHitCount == source.HitDamages.Count);
                if (keepSourceHitDamages)
                    return source;

                int targetCount = resolvedHitCount > 0 ? resolvedHitCount : source.HitDamages.Count;
                var scaledHitDamages = allowReusableList ? reusableModifiedHitDamages : new List<float>(targetCount);
                if (allowReusableList)
                {
                    scaledHitDamages.Clear();
                    if (scaledHitDamages.Capacity < targetCount)
                        scaledHitDamages.Capacity = targetCount;
                }

                // 기존 히트별 데미지 스케일 적용
                for (int i = 0; i < source.HitDamages.Count; i++)
                {
                    scaledHitDamages.Add(source.HitDamages[i] * resolvedDamageScale);
                }

                // 히트 수 오버라이드 시 균등 데미지 재구성
                if (resolvedHitCount > 0 && resolvedHitCount != scaledHitDamages.Count)
                {
                    float perHitDamage = scaledHitDamages.Count > 0
                        ? scaledHitDamages[0]
                        : sourceBaseDamage * resolvedDamageScale;
                    scaledHitDamages.Clear();
                    for (int i = 0; i < resolvedHitCount; i++)
                    {
                        scaledHitDamages.Add(perHitDamage);
                    }
                }

                float firstDamage = scaledHitDamages.Count > 0
                    ? scaledHitDamages[0]
                    : sourceBaseDamage * resolvedDamageScale;
                return new AttackSource(source.Attacker, null, firstDamage, source.DamageType, source.AttackInstanceId,
                    scaledHitDamages, source.Skill);
            }

            // 단일 베이스 데미지 소스 + 다중 히트 재구성 경로
            if (resolvedHitCount > 1)
            {
                float perHitDamage = sourceBaseDamage * resolvedDamageScale;
                var hitDamages = allowReusableList ? reusableModifiedHitDamages : new List<float>(resolvedHitCount);
                if (allowReusableList)
                {
                    hitDamages.Clear();
                    if (hitDamages.Capacity < resolvedHitCount)
                        hitDamages.Capacity = resolvedHitCount;
                }

                for (int i = 0; i < resolvedHitCount; i++)
                {
                    hitDamages.Add(perHitDamage);
                }

                return new AttackSource(source.Attacker, null, perHitDamage, source.DamageType, source.AttackInstanceId,
                    hitDamages, source.Skill);
            }

            // 단일 히트 기본 경로
            float scaledBaseDamage = sourceBaseDamage * resolvedDamageScale;
            return new AttackSource(source.Attacker, null, scaledBaseDamage, source.DamageType, source.AttackInstanceId,
                null, source.Skill);
        }
    }
}

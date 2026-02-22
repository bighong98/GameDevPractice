using System;
using System.Collections.Generic;
using UnityEngine;
using TH.Attribute;
using TH.Resource;

namespace TH.Combat
{
    // 스킬 선택/소비/해석 상태 제어 인터페이스
    public interface ISkillController
    {
        // 활성 스킬 변경 알림 이벤트
        event Action<SkillTypeSO> OnActiveSkillChanged;
        // 실제 실행 예정 스킬 변경 알림 이벤트
        event Action<SkillTypeSO> OnResolvedSkillChanged;
        // 쿨다운 종료 준비 완료 알림 이벤트
        event Action<SkillTypeSO> OnSkillReady;
        // 등록 스킬 목록 변경 알림 이벤트
        event Action OnSkillBookChanged;
        // 사용 가능 스킬 목록 변경 알림 이벤트
        event Action OnAvailableSkillsChanged;
        // 슬롯별 스킬 변경 알림 이벤트
        event Action<int, SkillTypeSO> OnSkillSlotChanged;
        // 콤보 단계 변경 알림 이벤트
        event Action<SkillTypeSO, int, int> OnComboStepChanged;
        // 슬롯 하이라이트 요청 알림 이벤트
        event Action<SkillTypeSO> OnSkillSlotHighlightRequested;

        // 활성 스킬 보유 상태
        bool HasActiveSkill { get; }
        // 현재 활성 스킬 참조
        SkillTypeSO ActiveSkill { get; }
        // 해석 완료 스킬 보유 상태
        bool HasResolvedSkill { get; }
        // 현재 해석 완료 스킬 참조
        SkillTypeSO ResolvedSkill { get; }
        // 현재 실행 고정 스킬 보유 상태
        bool HasExecutingSkill { get; }
        // 현재 실행 고정 스킬 참조
        SkillTypeSO ExecutingSkill { get; }
        // 활성 스킬 즉시 사용 가능 상태
        bool IsActiveSkillReady { get; }
        // 보류 공격 존재 상태
        bool HasPendingAttack { get; }
        // 현재 프리뷰 기준 유효 사거리
        float ActiveSkillRange { get; }
        // 현재 콤보 단계 인덱스
        int CurrentComboStepIndex { get; }
        // 현재 콤보 총 단계 수
        int CurrentComboStepCount { get; }
        // 등록 스킬 읽기 전용 목록
        IReadOnlyList<SkillTypeSO> RegisteredSkills { get; }
        // 사용 가능 스킬 읽기 전용 목록
        IReadOnlyList<SkillTypeSO> AvailableSkills { get; }
        // 슬롯 정렬 반영 사용 가능 스킬 읽기 전용 목록
        IReadOnlyList<SkillTypeSO> OrderedAvailableSkills { get; }

        // 스킬 등록 처리
        bool RegisterSkill(SkillTypeSO skill, bool setActive = false);
        // 활성 스킬 교체 처리
        bool SetActiveSkill(SkillTypeSO skill);
        // 필요 시점에 활성 스킬을 지연 선택/보정
        bool TryRequestActiveSkill();
        // 사용 가능 스킬 변경 적용(교체 미지정 시 하이라이트 요청 처리)
        bool ApplySkillAvailabilityChange(SkillTypeSO targetSkill, SkillTypeSO replacementSkill = null);
        // 슬롯 인덱스 기반 스킬 조회
        bool TryGetOrderedSkillAt(int slotIndex, out SkillTypeSO skill);
        // 스킬 기준 슬롯 인덱스 조회
        int FindOrderedSkillSlotIndex(SkillTypeSO skill);
        // 스킬 잔여 쿨다운 조회
        float GetRemainingCooldown(SkillTypeSO skill);
        // 활성 시퀀스 타임아웃 조회
        bool TryGetActiveSequenceTimeout(SkillTypeSO skill, out float remainingTimeout, out float totalTimeout);
        // 활성 스킬 소비 + 공격 소스 생성
        bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource);
        // 소비 없는 프리뷰 공격 소스 생성
        bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource);
        // 보류 공격 실행 시도
        bool TryExecutePendingAttack(IAttacker attacker, Health target);
        // stale 판단으로 보류 공격 취소 시도
        bool TryCancelPendingAttackIfStale();
        // 투사체 실행기 등록
        void SetProjectileExecutor(ISkillProjectileExecutor executor);
        // 투사체 실행기 해제
        void ClearProjectileExecutor(ISkillProjectileExecutor executor);
    }

    // 투사체 실행 위임 인터페이스
    public interface ISkillProjectileExecutor
    {
        // 투사체 실행 요청 처리
        bool TryExecuteProjectile(in AttackSource attackSource, Health target, SkillTypeSO skill);
    }
}

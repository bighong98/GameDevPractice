using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    // 활성 스킬이 사용 가능하고 타겟이 유효한지 판단하는 상태 조건 SO
    [CreateAssetMenu(fileName = "ActiveSkillReadyConditionSO", menuName = "Scriptable Objects/State Condition/Skill/ActiveSkillReadyConditionSO")]
    public class ActiveSkillReadyConditionSO : ActionStateConditionSO
    {
        // 현재 프레임 기준 즉시 판정
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IAttacker attacker)) return base.Decide(controller);
            if (!controller.Components.TryGet(out ISkillController skillController)) return base.Decide(controller);

            return attacker.IsTargetValid &&
                   skillController.HasActiveSkill &&
                   skillController.IsActiveSkillReady;
        }

        // 관련 이벤트를 구독해 조건이 충족될 때 onTriggered를 호출
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out IAttacker attacker))
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out ISkillController skillController))
                return base.Bind(controller, onTriggered);

            skillController.OnSkillReady += HandleSkillReady;
            attacker.OnTargetSet += HandleTargetSet;
            attacker.OnAttackReady += HandleAttackReady;

            // 바인딩 해제 시 이벤트 누수를 막기 위해 반드시 구독을 해제
            return new DisposableDelegate(() =>
            {
                if (skillController.IsNotNull())
                    skillController.OnSkillReady -= HandleSkillReady;
                if (attacker.IsNotNull())
                    attacker.OnTargetSet -= HandleTargetSet;
                if (attacker.IsNotNull())
                    attacker.OnAttackReady -= HandleAttackReady;
            });

            // 스킬 Ready 이벤트가 활성 스킬 대상이고 타겟이 유효할 때 트리거
            void HandleSkillReady(SkillTypeSO skill)
            {
                if (skillController.ActiveSkill != skill) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            // 타겟이 새로 잡혔을 때 이미 공격 가능 조건이면 즉시 트리거
            void HandleTargetSet(Health _)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            // 파이터가 AttackReady를 발행한 경우 동일 조건을 다시 확인하고 트리거
            void HandleAttackReady()
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }
        }
    }
}

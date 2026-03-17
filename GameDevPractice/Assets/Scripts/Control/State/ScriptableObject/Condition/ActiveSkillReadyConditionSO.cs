// 활성 스킬 준비 전이 조건 에셋 스크립트
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
        [SerializeField] private bool triggerOnAttackReady = true;
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
            if (triggerOnAttackReady)
            {
                attacker.OnAttackReady += HandleAttackReady;
            }

            return new DisposableDelegate(() =>
            {
                if (skillController.IsNotNull())
                    skillController.OnSkillReady -= HandleSkillReady;
                if (attacker.IsNotNull())
                {
                    attacker.OnTargetSet -= HandleTargetSet;

                    if (triggerOnAttackReady && attacker.IsNotNull())
                        attacker.OnAttackReady -= HandleAttackReady;
                }
                
            });

            void HandleSkillReady(SkillTypeSO skill)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (skillController.ActiveSkill != skill) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            void HandleTargetSet(Health _)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            void HandleAttackReady()
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }
        }
    }
}

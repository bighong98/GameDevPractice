// 타겟 설정 전이 조건 에셋 스크립트
using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "TargetSetConditionSO", menuName = "Scriptable Objects/State Condition/TargetSetConditionSO")]
    // TargetSetConditionSO 상태 전이 판단 조건 ScriptableObject
    public class TargetSetConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IAttacker attacker)) return base.Decide(controller);

            if (controller.Components.TryGet(out ISkillController skillController) &&
                skillController.HasExecutingSkill)
            {
                return false;
            }

            return attacker.IsTargetValid;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out IAttacker attacker) ||
                !controller.Components.TryGet(out ISkillController skillController) ||
                onTriggered == null)
                return base.Bind(controller, onTriggered);
            ;

            attacker.OnTargetSet += Handler;

            return new DisposableDelegate(() =>
            {
                if (attacker.IsNotNull())
                    attacker.OnTargetSet -= Handler;
            });
            
            void Handler(Health h)
            {
                if (!h.IsNotNull()) return;
                if (!attacker.IsTargetValid) return;
                if (skillController.IsNotNull() && skillController.HasExecutingSkill) return;

                onTriggered.Invoke();
            }
        }
    }
}


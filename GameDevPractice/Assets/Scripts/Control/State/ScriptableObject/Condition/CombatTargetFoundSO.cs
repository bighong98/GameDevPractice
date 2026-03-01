// 전투 타겟 탐지 전이 조건 에셋 스크립트
using UnityEngine;
using System;
using TH.Control.Movement;
using TH.Control.State;

namespace TH.Control.Data
{
    // [CreateAssetMenu(fileName = "CombatTargetFoundSO",
    //     menuName = "Scriptable Objects/State Condition/CombatTargetFoundSO")]
    public class CombatTargetFoundSO : ActionStateConditionSO
    {
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || !onTriggered.IsNotNull()
                                        || !controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);
            
            return base.Bind(controller, onTriggered);
        }
    }
}


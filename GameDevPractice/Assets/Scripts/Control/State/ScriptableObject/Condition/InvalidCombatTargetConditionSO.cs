// 전투 타겟 유효성 실패 조건 에셋 스크립트
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InvalidCombatTargetConditionSO", menuName = "Scriptable Objects/State Condition/InvalidCombatTargetConditionSO")]
    // InvalidCombatTargetConditionSO 상태 전이 판단 조건 ScriptableObject
    public class InvalidCombatTargetConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (controller == null) return false;
            if (!controller.Components.TryGet(out IFighter fighter)) return false;

            return !fighter.IsTargetValid;
        }
    }
}


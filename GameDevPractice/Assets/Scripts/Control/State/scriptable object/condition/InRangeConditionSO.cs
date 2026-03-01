using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InRangeConditionSO", menuName = "Scriptable Objects/State Condition/InRangeConditionSO")]
    // InRangeConditionSO 상태 전이 판단 조건 ScriptableObject
    public class InRangeConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attacker)) return false;
            
            return attacker.IsTargetInRange;
        }
    }
}


using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "OutOfInRangeConditionSO", menuName = "Scriptable Objects/State Condition/OutOfInRangeConditionSO")]
    // OutOfInRangeConditionSO 상태 전이 판단 조건 ScriptableObject
    public class OutOfInRangeConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attacker)) return false;
            
            return !attacker.IsTargetInRange;
        }
    }
}


using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InRangeConditionSO", menuName = "Scriptable Objects/State Condition/InRangeConditionSO")]
    public class InRangeConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Fighter fighter)) return false;
            
            return fighter.IsInRange;
        }
    }
}


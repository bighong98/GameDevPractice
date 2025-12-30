using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InvalidCombatTargetConditionSO", menuName = "Scriptable Objects/State Condition/InvalidCombatTargetConditionSO")]
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


using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InValidTargetConditionSO", menuName = "Scriptable Objects/State Condition/InValidTargetConditionSO")]
    public class InValidTargetConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (controller == null) return false;
            if (!controller.Components.TryGet(out IFighter fighter)) return false;

            return !fighter.IsTargetValid;
        }
    }
}


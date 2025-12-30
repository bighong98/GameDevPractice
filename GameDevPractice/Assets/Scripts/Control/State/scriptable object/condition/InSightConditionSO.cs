using UnityEngine;
using TH.Combat;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InSightConditionSO", menuName = "Scriptable Objects/State Condition/InSightConditionSO")]
    public class InSightConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attacker) ||
                !controller.Components.TryGet(out Transform trs) ||
                !controller.Components.TryGet(out ISightHandler sightHandler) ||
                !attacker.IsTargetValid) return false;

            return Vector3.SqrMagnitude(trs.position - attacker.Target.transform.position) < sightHandler.SightThreshold;
        }
    }
}


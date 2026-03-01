using UnityEngine;
using TH.Control.State;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InValidPathConditionSO", menuName = "Scriptable Objects/State Condition/InValidPathConditionSO")]
    // InValidPathConditionSO 상태 전이 판단 조건 ScriptableObject
    public class InValidPathConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (controller == null) return false;
            if (!controller.Components.TryGet(out NavMeshAgent navMeshAgent)) return false;

            return navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid
                   || (!navMeshAgent.hasPath && !navMeshAgent.pathPending);
        }
    }
}


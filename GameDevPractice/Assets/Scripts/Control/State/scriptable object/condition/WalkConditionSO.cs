using TH.Control.State;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "WalkConditionSO", menuName = "Scriptable Objects/State Condition/WalkConditionSO")]
    public class WalkConditionSO : ActionStateConditionSO
    {
        private static readonly float WalkThreshold = 0.5f; 
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out NavMeshAgent navMeshAgent)) return false;

            return navMeshAgent.isOnNavMesh
                   && !navMeshAgent.isStopped
                   && navMeshAgent.hasPath
                   && !navMeshAgent.pathPending
                   && navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid
                   && navMeshAgent.velocity.sqrMagnitude > WalkThreshold;


            // return navMeshAgent.isOnNavMesh 
            //     && navMeshAgent.velocity.sqrMagnitude > WalkThreshold;
        }
    }
}


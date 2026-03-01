// 이동 정지 판정 전이 조건 에셋 스크립트
using TH.Control.State;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "StopConditionSO", menuName = "Scriptable Objects/State Condition/StopConditionSO")]
    // StopConditionSO 상태 전이 판단 조건 ScriptableObject
    public class StopConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out NavMeshAgent navMeshAgent)) return false;
            return navMeshAgent.isStopped;
        }
    }
}


using TH.Control.State;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ResetPathActionSO", menuName = "Scriptable Objects/CharacterAction/ResetPathActionSO")]
    // ResetPathActionSO 상태 동작 실행 액션 ScriptableObject
    public class ResetPathActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out NavMeshAgent navMeshAgent)) return;
            
            navMeshAgent.ResetPath();
        }
    }
}

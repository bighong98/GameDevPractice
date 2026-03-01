using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "RunActionSO", menuName = "Scriptable Objects/CharacterAction/RunActionSO")]
    // RunActionSO 상태 동작 실행 액션 ScriptableObject
    public class RunActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Move(MoveType.Run);
        }
    }
}

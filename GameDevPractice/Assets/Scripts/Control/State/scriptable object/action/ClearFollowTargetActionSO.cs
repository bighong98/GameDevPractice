using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ClearFollowTargetActionSO", menuName = "Scriptable Objects/CharacterAction/ClearFollowTargetActionSO")]
    // ClearFollowTargetActionSO 상태 동작 실행 액션 ScriptableObject
    public class ClearFollowTargetActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Follow(null, stopIfInvalidTarget: false);
        }
    }
}
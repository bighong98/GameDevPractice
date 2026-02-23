using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ClearFollowTargetActionSO", menuName = "Scriptable Objects/CharacterAction/ClearFollowTargetActionSO")]
    public class ClearFollowTargetActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Follow(null, stopIfInvalidTarget: false);
        }
    }
}
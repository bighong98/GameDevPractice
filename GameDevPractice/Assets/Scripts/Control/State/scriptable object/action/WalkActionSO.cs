using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "WalkActionSO", menuName = "Scriptable Objects/CharacterAction/WalkActionSO")]
    public class WalkActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Move(MoveType.Walk);
        }
    }
}
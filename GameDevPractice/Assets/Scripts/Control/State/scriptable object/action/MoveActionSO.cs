using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "MoveAction", menuName = "Scriptable Objects/CharacterAction/MoveAction")]
    public class MoveActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out MoverRefactoring mover))
            {
                mover.Move();
            }
        }
    }
}

using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "StopActionSO", menuName = "Scriptable Objects/CharacterAction/StopActionSO")]
    public class StopActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out MoverRefactoring mover))
            {
                mover.Stop();
            }
        }
    }
}


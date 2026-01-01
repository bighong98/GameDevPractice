using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "StopWalkActionSO", menuName = "Scriptable Objects/CharacterAction/StopWalkActionSO")]
    public class StopWalkActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out Mover mover))
            {
                mover.Stop();
            }
        }
    }
}


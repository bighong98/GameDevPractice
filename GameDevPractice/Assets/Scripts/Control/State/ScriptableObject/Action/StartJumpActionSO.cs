using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "StartJumpActionSO", menuName = "Scriptable Objects/CharacterAction/StartJumpActionSO")]
    public sealed class StartJumpActionSO : CharacterActionSO
    {
        [SerializeField] private bool stopMoverOnJump = true;

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IJumpMotor jumpMotor))
            {
                return;
            }

            bool started = jumpMotor.TryStartJump();
            if (!started || !stopMoverOnJump)
            {
                return;
            }

            if (controller.Components.TryGet(out IMover mover))
            {
                mover.Stop();
            }
        }
    }
}

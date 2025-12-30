using TH.Control.State;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateMoveVelocitySO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateMoveVelocitySO")]
    public class AnimateMoveVelocitySO : CharacterActionSO
    {
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out NavMeshAgent agent) ||
                !controller.Components.TryGet(out Animator anim) ||
                !controller.Components.TryGet(out Transform trs)) return;
            
            Vector3 velocity = agent.velocity;
            Vector3 localVelocity = trs.InverseTransformDirection(velocity);
            
            anim.SetFloat(ForwardSpeed, localVelocity.z);
            this.Log($"localVelocity.z: {localVelocity.z}", Logg.LoggingMode.Completed);
        }
    }

}

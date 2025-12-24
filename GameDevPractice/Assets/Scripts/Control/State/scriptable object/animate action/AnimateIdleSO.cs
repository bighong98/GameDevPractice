using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateIdleSO", menuName = "Scriptable Objects/AnimateAction/AnimateIdleSO")]
    public class AnimateIdleSO : CharacterActionSO
    {
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            
            animator.SetFloat(ForwardSpeed, 0f);
        }
    }
}


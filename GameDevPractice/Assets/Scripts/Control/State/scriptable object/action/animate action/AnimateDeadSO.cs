using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateDeadSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateDeadSO")]
    public class AnimateDeadSO : CharacterActionSO
    {
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            
            animator.SetTrigger(DieAnimHash);
        }
    }
}




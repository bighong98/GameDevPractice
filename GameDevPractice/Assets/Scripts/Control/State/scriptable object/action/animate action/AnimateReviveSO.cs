using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateReviveSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateReviveSO")]
    // AnimateReviveSO 애니메이션 제어 액션 ScriptableObject
    public class AnimateReviveSO : CharacterActionSO
    {
        private static readonly int ReviveAnimHash = Animator.StringToHash("revive");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            
            animator.SetTrigger(ReviveAnimHash);
        }
    }
}


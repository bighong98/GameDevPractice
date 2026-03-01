// 대기 애니메이션 제어 액션 에셋 스크립트
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateIdleSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateIdleSO")]
    // AnimateIdleSO 애니메이션 제어 액션 ScriptableObject
    public class AnimateIdleSO : CharacterActionSO
    {
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            
            animator.CrossFade(
                stateHashName: LocomotionASSHash,
                normalizedTransitionDuration: 0.1f,
                layer: AnimatorBaseLayer);
            animator.SetFloat(ForwardSpeed, 0f);
        }
    }
}


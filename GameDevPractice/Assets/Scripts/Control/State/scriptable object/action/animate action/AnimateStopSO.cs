using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateStopSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateStopSO")]
    // AnimateStopSO 애니메이션 제어 액션 ScriptableObject
    public class AnimateStopSO : CharacterActionSO
    {
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");
        private static readonly int AttackAnimHash = Animator.StringToHash("attack");
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        private static readonly int ReviveAnimHash = Animator.StringToHash("revive");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            
            // animator 트리거 리셋
            animator.ResetTrigger(DieAnimHash);
            animator.ResetTrigger(ReviveAnimHash);
            // 움직임에 영향을 주는 모든 animator 파라미터 덮어쓰기
            animator.SetFloat(ForwardSpeed, 0f);
        }
    }
}


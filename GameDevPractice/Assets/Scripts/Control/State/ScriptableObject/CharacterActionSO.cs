using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    // 한번 실행 시작 후 반드시 끝마쳐야할 작업이 있는 경우 IStateTransitionLock 인터페이스 구현해서 사용
    // IStateTransitionLock 사용 전 인터페이스 사용 시 주의사항 꼭 읽어볼 것
    public abstract class CharacterActionSO : ScriptableObject, ICharacterAction
    {
        public abstract void Execute(IActionStateController controller);


        protected static readonly int AnimatorBaseLayer = 0;
        // Animator.StringToHash()
        // 실제 파라미터 이름이 바뀌면 작동하지 않음에 주의
        protected static readonly int CancelAllowHash = Animator.StringToHash("CancelAllow");
        
        protected static readonly int AttackASSHash = Animator.StringToHash("Attack");
        protected static readonly int LocomotionASSHash =  Animator.StringToHash("Locomotion");
        protected static readonly int DeathASSHash =  Animator.StringToHash("Death");
        
        protected const float CancelAllowThreshold = 0.9f;
        protected const float AnimationEndThreshold = 0.95f;
    }
}


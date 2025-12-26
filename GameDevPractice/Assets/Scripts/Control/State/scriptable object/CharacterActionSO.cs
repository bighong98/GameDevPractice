using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    // 한번 실행 시작 후 반드시 끝마쳐야할 작업이 있는 경우 IStateTransitionLock 인터페이스 구현해서 사용
    // IStateTransitionLock 사용 전 인터페이스 사용 시 주의사항 꼭 읽어볼 것
    public abstract class CharacterActionSO : ScriptableObject, ICharacterAction
    {
        public abstract void Execute(IActionStateController controller);
        
        protected static readonly int CancelAllowHash = Animator.StringToHash("CancelAllow"); 
        protected static readonly float CancelAllowThreshold = 0.5f;
        protected static readonly float AnimationEndThreshold = 0.95f;
    }
}


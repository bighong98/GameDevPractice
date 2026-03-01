using System;

namespace TH.Control.State
{
    // 상태 객체 생명주기와 전이 훅을 정의하는 상태 계약 인터페이스
    public interface IActionState
    {
        bool AllowSelfTransition { get; } // 자기 자신으로의 상태 전환 허용 여부 (Global Transition에만 적용 중)
        bool TransitionLockRequired { get; } // 상태 전환 조건이 만족되어도 즉시 상태 전환하지 않고 지연이 필요한 경우
        
        void EnterState(IActionStateController controller); // 상태 진입 시 호출
        void UpdateState(IActionStateController controller); // 상태 유지 시 Update 주기마다 호출
        void ExitState(IActionStateController controller);  // 상태 퇴장 시 호출
        
        // event-driven 전이 조건 구독
        void BindTransitions(IActionStateController controller, Action<IDisposable> register); 
        
        // TransitionLockRequired: true 일 때에 사용
        IDisposable BindTransitionUnlock(IActionStateController controller, Action register);
    }
}
using System;

namespace TH.Control.State
{
    public interface IActionState
    {
        bool AllowSelfTransition { get; }
        
        void EnterState(IActionStateController controller); // 상태 진입 시 호출
        void UpdateState(IActionStateController controller); // 상태 유지 시 Update 주기마다 호출
        void ExitState(IActionStateController controller);  // 상태 퇴장 시 호출

        void BindTransitions(IActionStateController controller, Action<IDisposable> register); // event-driven 전이 조건 구독
    }
}
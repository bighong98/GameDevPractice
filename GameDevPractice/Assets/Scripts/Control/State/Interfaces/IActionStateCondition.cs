using System;

namespace TH.Control.State
{
    // 상태 전이 조건 평가와 이벤트 바인딩 규약 계약 인터페이스
    public interface IActionStateCondition: IPollingStateCondition, IEventStateCondition
    {
        // 평가가 필요한 조건 (StateConditionMeasures 주석 참고)
        StateConditionMeasures Measure { get; }
        
        // bool Decide(IActionStateController controller);
        // IDisposable Bind(IActionStateController controller, Action onTriggered);

#if UNITY_EDITOR
        // 수동 OnValidate() 호출 목적
        void Validate();
#endif
    }

    public interface IPollingStateCondition
    {
        // polling 방식
        // Decide 호출 즉시 조건 평가 및 결과 반환
        bool Decide(IActionStateController controller);
    }

    public interface IEventStateCondition
    {
        // event-driven 방식
        // onTriggered에 TransitionState()를 전달하여 원하는 시점에 상태 전환 실행
        // -> .Dispose()로 이벤트 핸들러 포함 내부 클로저 정리 수행할 수 있도록 구현
        IDisposable Bind(IActionStateController controller, Action onTriggered);
    }
    
    public enum StateConditionMeasures
    {
        Both, // polling && event-driven (두 조건 모두 만족) -> 이벤트 호출 이후부터는 매프레임 polling 체크
        Polling, // polling only (매 프레임 검사가 필요한 경우 사용)
        EventDriven, // event-driven only
        EventTriggeredPolling // event-driven base + polling once after event called (이벤트 호출 뒤 -> Decide로 검사 한번 더 함)
    }
}


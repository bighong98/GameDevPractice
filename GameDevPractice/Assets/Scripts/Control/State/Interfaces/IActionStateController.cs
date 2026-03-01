// 상태 컨트롤러 계약 인터페이스 스크립트
using System.Threading;
using System;
using TH.Utils;

namespace TH.Control.State
{
    // 상태 전이 실행과 컴포넌트 접근을 제공하는 컨트롤러 계약 인터페이스
    public interface IActionStateController
    {
        ComponentProvider Components { get; } // 캐릭터 컴포넌트 조회
        CancellationToken StateToken { get; } // 현재 상태 종료 감지 토큰 (-> 상태에 연결된 Action, Condition 내부 작업 정리 목적)
        bool IsTransitionLocked { get; } // 전이 잠금 활성 여부 확인 프로퍼티
        
        // 다음 상태 전환 요청 API
        void TransitionToState(IActionState nextState, bool ignoreLock = false);
        // 런타임 전이 잠금 획득 API
        IDisposable AcquireTransitionLock(object owner = null);
        // 이벤트 트리거 조건 기반 전이 처리 API
        void HandleConditionTriggered(
            IActionStateCondition condition,
            IActionState destination,
            bool isGlobal,
            bool ignoreForce = false);
    }
}


using System.Threading;
using TH.Control.Data;
using TH.Utils;
using UnityEngine;
using System;

namespace TH.Control.State
{
    // 상태 전이 실행과 컴포넌트 접근을 제공하는 컨트롤러 계약 인터페이스
    public interface IActionStateController
    {
        ComponentProvider Components { get; }
        CancellationToken StateToken { get; }
        bool IsTransitionLocked { get; }
        
        void TransitionToState(IActionState nextState, bool ignoreLock = false);
        IDisposable AcquireTransitionLock(object owner = null);
        void HandleConditionTriggered(
            IActionStateCondition condition,
            IActionState destination,
            bool isGlobal,
            bool ignoreForce = false);
    }
}


// 상태 전이 잠금 계약 인터페이스 스크립트
using UnityEngine;
using System;

namespace TH.Control.State
{
    // FSM에서 관리하는 상태의 동작(ICharacterAction(CharacterActionSO)) 중에서
    // 실행 완료가 보장되어야 할 작업이 있는 경우 구현해서 사용
    // BindMinimumCompleted 구현 시 반드시 적절한 타이밍에 onCompleted.Invoke(); 호출되어야함
    // -> onCompleted.Invoke(); 호출되지 않으면 FSM이 영원히 멈춤
    public interface IStateTransitionLock
    {
        bool TransitionLockRequired { get; } // true인 경우에만 대기 조건에 반영됨
        IDisposable BindMinimumCompleted(IActionStateController controller, Action onCompleted);
    }
}


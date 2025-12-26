using System;
using UnityEngine;
using TH.Control.State;
using TH.Utils;

namespace TH.Control.Data
{
    // [CreateAssetMenu(fileName = "ActionStateConditionSO", menuName = "Scriptable Objects/Action/ActionStateConditionSO")]
    public abstract class ActionStateConditionSO : ScriptableObject, IActionStateCondition
    {
        // polling 방식으로 조건 검사가 필요한 경우 사용
        // 미사용 시 항상 false 반환하도록 구현
        public abstract bool Decide(IActionStateController controller);
        // event-driven 방식으로 조건 검사가 필요한 경우 사용
        // 내부적으로 구독 해제용 이벤트 핸들러를 캐싱
        // -> Dispose 호출하여 구독해제 실행 및 내부 핸들러 정리
        // 미사용 시 Empty 객체 (내부 핸들러 없는 빈 객체) 반환하도록 구현
        // -> 상속 클래스에서 return base.Bind(controller, onTriggered);
        // -> 해당 ConditionSO의 의존 컴포넌트가 없는 경우에도 마찬가지로 Empty 반환하도록 구현
        public virtual IDisposable Bind(IActionStateController controller, Action onTriggered) 
            => DisposableDelegate.Empty;
    }
}

using System;
using System.Reflection;
using UnityEngine;
using TH.Control.State;
using TH.Utils;

namespace TH.Control.Data
{
    public abstract class ActionStateConditionSO : ScriptableObject, IActionStateCondition
    {
        // 상태 전환 조건 평가 기준
        // Both: Decide(polling) + Bind(event-driven) 둘다 사용 -> default
        // Polling: Decide()만 사용
        // EventDriven: Bind()만 사용
        [SerializeField] protected StateConditionMeasures measure = StateConditionMeasures.Both;
        public StateConditionMeasures Measure => measure;
        
        // polling 방식(호출 즉시 조건 검사 결과 반환)
        // -> 매 프레임 확인해야하는 경우 (StateConditionMeasures.Polling)
        // -> 이벤트 기반 조건에 부가 조건을 추가해야하는 경우 (StateConditionMeasures.Both)
        // 미사용 시 항상 false 반환 (사용 시 override 해서 사용)
        public virtual bool Decide(IActionStateController controller)
            => false;
        
        // event-driven 방식 (상태 전환과 동시에 필요한 이벤트를 구독하여 상태 전환을 요청)
        // 
        // 내부적으로 구독 해제용 이벤트 핸들러를 캐싱
        // -> Dispose 호출하여 구독해제 실행 및 내부 핸들러 정리
        // 미사용 시 Empty 객체 (내부 핸들러 없는 빈 객체) 반환하도록 구현
        // -> 상속 클래스에서 return base.Bind(controller, onTriggered);
        // -> 해당 ConditionSO의 의존 컴포넌트가 없는 경우(StateConditionMeasures.Polling)에도 Empty 반환하도록 구현
        public virtual IDisposable Bind(IActionStateController controller, Action onTriggered) 
            => DisposableDelegate.Empty;

        #region Debug (Editor Only)

#if UNITY_EDITOR

        [ContextMenu("Validate")]
        public void Validate() => OnValidate();
        
        // ActionStateConditionSO의 유효성 검사
        // StateConditionMeasures.Both/EventTriggeredPolling -> Decide(), Bind() 둘다 오버라이드 매서드 있어야함
        // StateConditionMeasures.Polling -> Decide()에 오버라이드 매서드 있어야함 (Bind()는 신경x)
        // StateConditionMeasures.EventDriven -> Bind()에 오버라이드 매서드 있어야함 (Decide()는 신경x)
        protected virtual void OnValidate()
        {
            var t = GetType();

            bool overridesDecide = HasOverride(t, nameof(Decide),
                new[] { typeof(IActionStateController) });

            bool overridesBind = HasOverride(t, nameof(Bind),
                new[] { typeof(IActionStateController), typeof(Action) });

            switch (measure)
            {
                case StateConditionMeasures.Both:
                case StateConditionMeasures.EventTriggeredPolling:
                    if (!overridesDecide || !overridesBind)
                    {
                        Logg.LogError(
                            $"[ActionStateConditionSO] '{name}' ({t.Name}) measure={measure} requires override of BOTH Decide() and Bind(). " +
                            $"(Decide:{overridesDecide}, Bind:{overridesBind})", this);
                    }
                    break;

                case StateConditionMeasures.Polling:
                    if (!overridesDecide)
                    {
                        Logg.LogError(
                            $"[ActionStateConditionSO] '{name}' ({t.Name}) measure={measure} requires override of Decide().", this);
                    }
                    break;

                case StateConditionMeasures.EventDriven:
                    if (!overridesBind)
                    {
                        Logg.LogError(
                            $"[ActionStateConditionSO] '{name}' ({t.Name}) measure={measure} requires override of Bind().", this);
                    }
                    break;

                default:
                    Logg.LogWarning(
                        $"[ActionStateConditionSO] '{name}' ({t.Name}) has unknown measure value: {measure}", this);
                    break;
            }
        }

        private static bool HasOverride(Type runtimeType, string methodName, Type[] parameterTypes)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public ;

            var mi = runtimeType.GetMethod(methodName, flags, binder: null, types: parameterTypes, modifiers: null);
            if (mi == null) return false;

            // DeclaringType이 베이스(ActionStateConditionSO) 타입이면 override 안 한 것으로 간주
            return mi.DeclaringType != typeof(ActionStateConditionSO);
        }
#endif

        #endregion
    }
}

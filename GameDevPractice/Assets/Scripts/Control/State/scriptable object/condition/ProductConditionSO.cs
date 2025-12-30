using System;
using System.Collections.Generic;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    // 단일 조건만으로 표현하기 어려운 복잡한 상태 전환 조건을 구성하기 위한 복합 조건 클래스
    // - conditions 목록의 모든 조건이 true일 때만 전체가 true (AND 연산)
    // - Decide(): 모든 하위 조건의 Decide()가 true여야 true 반환 (Measure.OnEvent는 무시)
    // - Bind(): 하위 조건 중 하나라도 이벤트 트리거되면 전체 AND 재평가 (Measure.Polling은 무시)
    
    // [Measure(StateConditionMeasures) 동작]
    /// - Polling: Decide()만 사용 (하위 조건의 Bind() 무시)
    /// - EventDriven: Bind()만 사용 (하위 조건의 Decide() 무시)
    /// - Both/EventTriggeredPolling: Decide()와 Bind() 모두 활용
    /// -> Both: 이벤트 트리거 시 매 프레임 Decide() 호출 조건 검사 시작 (Polling과 유사하게 동작)
    /// -> EventTriggeredPolling: 이벤트 트리거 시 1회만 Decide() 호출 조건 검사 (실패할 경우 상태 전환x)

    /// [주의사항]
    /// - conditions 목록에 자기 자신(this)을 절대 포함하지 말 것 → 무한 재귀 발생
    [CreateAssetMenu(fileName = "ProductConditionSO", menuName = "Scriptable Objects/State Condition/ProductConditionSO")]
    public sealed class ProductConditionSO : ActionStateConditionSO
    {
        // AND 조합할 하위 조건 목록.
        [SerializeField] private List<ActionStateConditionSO> conditions = new();

        #region Decide - Polling 방식 조건 평가
        
        public override bool Decide(IActionStateController controller)
        {
            // 조건 기본 유효성 검사
            if (controller == null) return false;
            if (conditions == null || conditions.Count == 0) return false;
            
            // measure가 EventDriven이면 Decide()는 사용하지 않음
            // -> 이벤트 기반으로만 동작하므로 Polling 평가 스킵
            if (measure == StateConditionMeasures.EventDriven) return false;
            
            foreach (var c in conditions)
            {
                if (c == null) continue;
                
                // EventDriven 타입 조건은 Decide()가 항상 false 반환
                // -> AND 연산에서 제외해야 정상 동작
                if (c.Measure == StateConditionMeasures.EventDriven) continue;
                
                // 조건 중 하나라도 false면 즉시 전체 결과 false 반환
                if (!c.Decide(controller)) return false; 
            }

            // 모든 조건이 true (또는 EventDriven) -> 전체 true
            return true;
        }
        
        #endregion

        #region Bind - Event-Driven 방식 조건 구독
        
        // Event-Driven 방식
        // 하위 조건들의 이벤트를 구독 및 구독 해제 핸들러 반환
        // -> 상태 전환 후 .Dispose() 호출로 모든 구독 일괄 정리
        // 이벤트 트리거 시 전체 AND 재평가
        
        // 호출 시점: 상태 진입 시 1회 호출
        // -> ActionStateSO.BindTransitions() 또는 ActionStateMachine.BindTransitionList()
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            // 필수 파라미터 null 체크
            if (controller == null || onTriggered == null) return DisposableDelegate.Empty;
            // 조건 목록이 비어있으면 구독할 대상 없음
            if (conditions == null || conditions.Count == 0) return DisposableDelegate.Empty;
            // measure가 Polling이면 이벤트 구독 불필요
            if (measure == StateConditionMeasures.Polling) return DisposableDelegate.Empty;
            
            // fired: 이미 상태 전환이 트리거되었는지 여부
            // -> true면 이후 이벤트 무시 (한 번만 전환되도록 보장)
            bool fired = false;
            
            
            // 하위 조건 중 하나라도 이벤트 트리거 시 호출되는 공유 콜백
            // 전체 AND 조건을 재평가하고, 만족 시 상태 전환 요청
            void OnAnyChanged()
            {
                if (fired) return;
                
                // 전체 AND 조건 재평가
                // -> 이벤트 트리거된 조건 외 나머지 조건도 모두 만족해야 함
                if (!Decide(controller)) return;
                
                // 모든 조건 만족 -> 상태 전환 트리거
                fired = true;
                onTriggered.Invoke();
            }
            
            // 구독 해제용 핸들러 목록 (상태 전환 시 일괄 Dispose)
            var disposables = new List<IDisposable>(conditions.Count);

            foreach (var c in conditions)
            {
                if (c == null) continue;
                
                // Polling 타입은 이벤트 미지원 -> Bind() 호출 불필요
                if (c.Measure == StateConditionMeasures.Polling) continue;
                
                // 하위 조건에 공유 콜백(OnAnyChanged) 등록
                // -> 조건이 이벤트 미지원이면 base(DisposableDelegate.Empty) 반환
                var d = c.Bind(controller, OnAnyChanged);
                if (d != null)
                    disposables.Add(d);
            }
            
            // 구독된 이벤트가 없으면 Empty 반환
            // (모든 하위 조건이 Polling이거나 Bind() 미지원인 경우)
            if (disposables.Count == 0)
                return DisposableDelegate.Empty;
            
            // DisposableDelegate 반환
            // -> .Dispose() 호출 시 모든 하위 구독 일괄 해제
            return new DisposableDelegate(() =>
            {
                foreach (var d in disposables)
                {
                    try { d?.Dispose(); }
                    catch (Exception e) { Logg.LogError($"[ConditionAll] dispose error - {e}"); }
                }
                disposables.Clear();
            });
        }
        
        #endregion

        #region Editor Validation
        
#if UNITY_EDITOR
        // Inspector에서 값 변경 시 유효성 검사
        // [검사 항목]
        // 1. conditions 목록에 null(빈 슬롯) 경고
        // 2. conditions 목록에 자기 자신(this) 포함 여부 → 자동 제거 + 에러 로그
        // 3. 각 하위 조건의 Validate() 재귀 호출
        protected override void OnValidate()
        {
            // 부모 클래스(ActionStateConditionSO)의 Decide/Bind 오버라이드 검증
            base.OnValidate();
            
            if (conditions == null) return;
            for (int i = conditions.Count - 1; i >= 0; i--)
            {
                if (conditions[i] == null)
                {
                    Logg.LogWarning($"[ProductConditionSO] '{name}' has null child at index {i}. It will be ignored.", this);
                    continue;
                }

                // 자기 참조 체크: 무한 재귀 방지를 위해 자동 제거
                if (conditions[i] == this)
                {
                    conditions.RemoveAt(i);
                    Logg.LogError($"[ConditionAll] '{name}' cannot reference itself.", this);
                }
                
                // 하위 조건 유효성 검사 호출
                conditions[i].Validate();
            }
        }
#endif
        
        #endregion
    }
}

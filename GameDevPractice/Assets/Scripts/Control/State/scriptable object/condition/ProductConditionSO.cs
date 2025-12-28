using System;
using System.Collections.Generic;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    // 복수의 ConditionSO 조합에 사용 
    // conditions 목록의 모든 조건이 true일 때만 true
    // -> Polling: 하위 조건의 Bind() 무시
    // -> EventDriven: 하위 조건의 Decide 무시
    // 절대 conditions 목록에 자기 자신을 포함하지 않을 것
    [CreateAssetMenu(fileName = "ProductConditionSO", menuName = "Scriptable Objects/State Condition/ProductConditionSO")]
    public sealed class ProductConditionSO : ActionStateConditionSO
    {
        [SerializeField] private List<ActionStateConditionSO> conditions = new();

        public override bool Decide(IActionStateController controller)
        {
            if (controller == null) return false;
            if (conditions == null || conditions.Count == 0) return false;
            if (measure == StateConditionMeasures.EventDriven) return false;
            
            foreach (var c in conditions)
            {
                if (c == null) continue;
                // EventDriven 타입은 조건 평가에서 제외 (무조건 false 반환함)
                if (c.Measure == StateConditionMeasures.EventDriven) continue;
                // 조건 중 하나라도 null 이거나 false면 전체 false 반환
                if (!c.Decide(controller)) return false; 
            }

            return true;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            // 구독할 이벤트 기반 조건이 없다면 Empty 반환
            if (controller == null || onTriggered == null) return DisposableDelegate.Empty;
            if (conditions == null || conditions.Count == 0) return DisposableDelegate.Empty;
            if (measure == StateConditionMeasures.Polling) return DisposableDelegate.Empty;
            
            // 어떤 하위 조건이든 트리거되면 전체 AND 재평가 후 true면 fire
            bool fired = false;
            void OnAnyChanged()
            {
                if (fired) return;
                if (!Decide(controller)) return; // 전체 AND 만족 시에만
                fired = true;
                onTriggered.Invoke();
            }

            // 하위 이벤트 핸들러 모으기
            var disposables = new List<IDisposable>(conditions.Count);

            foreach (var c in conditions)
            {
                if (c == null) continue;
                if (c.Measure == StateConditionMeasures.Polling) continue;
                
                // 이벤트 기반을 지원하지 않는 조건은 Empty를 반환
                var d = c.Bind(controller, OnAnyChanged);
                if (d != null)
                    disposables.Add(d);
            }

            // 구독할 이벤트 기반 조건이 없다면 Empty 반환
            if (disposables.Count == 0)
                return DisposableDelegate.Empty;
            
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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (conditions == null) return;
            // 조건 목록에 null(빈 조건) 포함 불가
            // 자기 자신은 포함 불가 (-> 포함시 콘솔에 에러 메시지 송출)
            for (int i = conditions.Count - 1; i >= 0; i--)
            {
                if (conditions[i] == null)
                {
                    Logg.LogWarning($"[ProductConditionSO] '{name}' has null child at index {i}. It will be ignored.", this);
                    continue;
                }

                if (conditions[i] == this)
                {
                    conditions.RemoveAt(i);
                    Logg.LogError($"[ConditionAll] '{name}' cannot reference itself.", this);
                }
                
                conditions[i].Validate();
            }
        }
#endif
    }
}

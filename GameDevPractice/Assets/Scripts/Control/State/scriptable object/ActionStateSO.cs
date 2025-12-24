using System;
using System.Collections.Generic;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    // [CreateAssetMenu(fileName = "ActionStateSO", menuName = "Scriptable Objects/Action/ActionStateSO")]
    public abstract class ActionStateSO : ScriptableObject, IActionState
    {
        [Header("Actions")]
        [SerializeField] private List<CharacterActionSO> onEnterActions; // 진입 시 1회
        [SerializeField] private List<CharacterActionSO> updateActions;  // 매 프레임
        [SerializeField] private List<CharacterActionSO> onExitActions;  // 퇴장 시 1회

        [Header("Transitions")]
        [SerializeField] private List<ActionStateTransition> transitions;

        #region IActionState

        public bool AllowSelfTransition => allowSelfTransition;
        [SerializeField] private bool allowSelfTransition = false;
        
        public void EnterState(IActionStateController controller)
        {
            ExecuteActions(controller, onEnterActions);
        }

        public void UpdateState(IActionStateController controller)
        {
            ExecuteActions(controller, updateActions);
            CheckTransitions(controller);
        }

        public void ExitState(IActionStateController controller)
        {
            ExecuteActions(controller, onExitActions);
        }
        
        public void BindTransitions(IActionStateController controller, Action<IDisposable> register)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (register == null) throw new ArgumentNullException(nameof(register));
            if (transitions == null || transitions.Count == 0) return;

            foreach (var t in transitions)
            {
                // 클로저 생성 (destination 캡처)
                var destination = t.DestinationState;
                if (destination == null) continue;

                if (t.Condition is not ActionStateConditionSO conditionSo) continue;
                
                // 이벤트 미지원 -> Disposable.Empty 반환
                var token = conditionSo.Bind(
                    controller,
                    onTriggered: () => controller.TransitionToState(destination)
                );
                
                if (token != null)
                    register(token);
            }
        }

        #endregion
        
        private void ExecuteActions(IActionStateController controller, List<CharacterActionSO> actionList)
        {
            if (actionList == null || actionList.Count == 0) return;
            foreach (var action in actionList)
            {
                action.Execute(controller);
            }
        }

        private void CheckTransitions(IActionStateController controller)
        {
            if (transitions == null || transitions.Count == 0) return;
            foreach (var transition in transitions)
            {
                if (!transition.Condition.Decide(controller)) continue;
                // 조건 충족 시 상태 전이 및 루프 종료
                controller.TransitionToState(transition.DestinationState);
                return; 
            }
        }
    }
}
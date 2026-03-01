// 상태 액션/전이 베이스 에셋 스크립트
using System;
using System.Collections.Generic;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ActionStateSO", menuName = "Scriptable Objects/CharacterState/ActionStateSO")]
    // 상태 액션 목록과 전이 목록을 보유하는 상태 ScriptableObject 베이스
    public class ActionStateSO : ScriptableObject, IActionState
    {
        [Header("Actions")]
        [SerializeField] private List<CharacterActionSO> onEnterActions; // 진입 시 1회
        [SerializeField] private List<CharacterActionSO> updateActions;  // 매 프레임
        [SerializeField] private List<CharacterActionSO> onExitActions;  // 퇴장 시 1회

        [Header("Transitions")]
        [SerializeField] private List<ActionStateTransition> transitions;

        [Header("Transition Settings")]
        [SerializeField] private bool allowSelfTransition = false;
        [SerializeField] private bool transitionLockRequired = false;
        
        #region IActionState
        
        // 용도: 동일 상태 재진입 허용 정책 노출 프로퍼티
        public bool AllowSelfTransition => allowSelfTransition;
        // 용도: 상태 진입 직후 전이 잠금 요구 정책 노출 프로퍼티
        public bool TransitionLockRequired => transitionLockRequired;
        
        // 상태 진입 시점 등록된 액션 일괄 실행
        public void EnterState(IActionStateController controller)
        {
            ExecuteActions(controller, onEnterActions);
        }

        // 상태 유지 중 매 프레임마다 호출 필요한 액션 일괄 실행
        // + 업데이트 액션 실행 후 Polling 전이 검사 책임
        public void UpdateState(IActionStateController controller)
        {
            ExecuteActions(controller, updateActions);
            CheckTransitions(controller);
        }

        // 종료 시점 정리 액션 일괄 실행
        public void ExitState(IActionStateController controller)
        {
            ExecuteActions(controller, onExitActions);
        }
        
        // EventDriven/Both 타입 상태 전이 조건(ConditionSO) 이벤트 구독
        // -> 상태 이탈 시 ActionStateMachine 측에서 내부 이벤트 정리 핸들러 (disposableHandler) 정리함
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
                // 상태 전환 조건 유효성 검사
                // Polling은 이벤트 지원x -> 스킵
                if (t.Condition is not {} condition
                    || !condition.IsNotNull()
                    || condition.Measure == StateConditionMeasures.Polling) continue;
                
                
                // 이벤트 미지원인 경우 DisposableDelegate.Empty 반환
                var disposeHandler = condition.Bind(
                    controller: controller,
                    onTriggered: () => controller.HandleConditionTriggered(
                            condition: condition, 
                            destination: destination,
                            isGlobal: false, ignoreForce: false)
                );
                
                if (disposeHandler != null)
                    register(disposeHandler);
            }
        }

        
        // 용도: 상태 진입 잠금 해제 조건 바인딩
        // -> lock action 완료 집계 후 register 콜백 호출로 내부 객체 정리 핸들러 전달
        public virtual IDisposable BindTransitionUnlock(IActionStateController controller, Action register)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (register == null) throw new ArgumentNullException(nameof(register));
            // 상태 전환 지연이 불필요한 경우
            // 빈 객체 반환 및 상태 전환 지연 즉시 중지
            if (!transitionLockRequired)
            {
                Logg.LogWarning($"[{GetType().Name}] transitionLockRequired is false but BindTransitionUnlock called");
                register.Invoke();
                return DisposableDelegate.Empty;
            }
            // 상태 전환 지연 조건들 수집
            var lockGates = new List<IStateTransitionLock>();
            CollectMinimumActions(onEnterActions, lockGates);
            CollectMinimumActions(updateActions, lockGates);
            CollectMinimumActions(onExitActions, lockGates);
            if (lockGates.Count == 0)
            {
                register.Invoke();
                return DisposableDelegate.Empty;
            }
            
            // 현재 상태 캡처 (클로저 생성)
            bool fired = false; // 이미 상태 전환 방지 뚫렸는지 여부
            int remaining = lockGates.Count; // 남은 상태 전환 방지 조건
            var disposables = new List<IDisposable>(lockGates.Count); // 개별 동작에 전달한 구독 해제용 헨들러 목록

            // 개별 동작(CharacterActionSO)에 상태 전환 지연이 필요한 작업 완료 이벤트 구독
            // disposables에 구독 해제용 핸들러를 담아서 상태 전환 후 정리
            foreach (var lockGate in lockGates)
            {
                bool completedOnce = false; // 클로저 생성 (중복 완료 호출 방지)
                IDisposable handler = lockGate.BindMinimumCompleted(controller, () =>
                {
                    if (fired || completedOnce) return;
                    completedOnce = true;
                    // 상태 전환 방지 조건이 모두 만료된 경우 -> 이벤트 핸들러 실행 -> 상태 전환 허락
                    remaining--;
                    if (remaining <= 0)
                        TryFire();
                });

                if (handler != null)
                    disposables.Add(handler);
            }

            // DisposableDelegate 객체 반환
            // 상태 전환이 이루어진 경우 -> Dispose() 호출로 내부 이벤트 헨들러 정리
            return new DisposableDelegate(() =>
            {
                foreach (var d in disposables)
                {
                    try { d?.Dispose(); }
                    catch (Exception e) { Logg.LogError($"[ActionStateSO.BindTransitionReady] dispose error - {e}"); }
                }

                disposables.Clear();
            });
            // 내부 이벤트 핸들러
            void TryFire()
            {
                if (fired) return;
                fired = true;
                this.Log($"transition unlocked", Logg.LoggingMode.Completed);
                register.Invoke();
            }
        }

        #endregion
        
        // 용도: 액션 리스트 공통 실행 유틸리티
        // -> null/빈 목록 가드 후 Execute 순차 호출 책임
        private void ExecuteActions(IActionStateController controller, List<CharacterActionSO> actionList)
        {
            if (actionList == null || actionList.Count == 0) return;
            foreach (var action in actionList)
            {
                action.Execute(controller);
            }
        }
        
        // 용도: Polling 기반 전이 평가 루프
        // -> 최초 충족 조건 발견 시 즉시 전이 후 루프 중단 정책
        // Polling 타입 상태 전환 조건 체크
        // Update 주기로 실행
        private void CheckTransitions(IActionStateController controller)
        {
            if (transitions == null || transitions.Count == 0) return;
            foreach (var transition in transitions)
            {
                // condition null 체크 (UnityEngine.Object 타입 널 체크 포함)
                if (transition.Condition is not { } condition) continue;
                if (!condition.IsNotNull()) continue;
                // Polling이 아니면 매 프레임 체크x
                if (condition.Measure != StateConditionMeasures.Polling) continue;
                if (!condition.Decide(controller)) continue;
                
                // 조건 충족 시 상태 전이 및 루프 종료
                this.Log($"CheckTransitions - Trying to TransitionToState from" +
                         $" condition: ({condition.GetType()}), state: {transition.DestinationState}", Logg.LoggingMode.Completed);
                controller.TransitionToState(transition.DestinationState);
                return; 
            }
        }
        
        // 용도: 전이 잠금 액션 추출 유틸리티
        // -> IStateTransitionLock 구현 + 활성 플래그 조건 필터링
        private static void CollectMinimumActions(List<CharacterActionSO> actions, List<IStateTransitionLock> dst)
        {
            if (actions == null || actions.Count == 0) return;

            foreach (var action in actions)
            {
                if (action is not IStateTransitionLock lockAction) continue;
                if (!lockAction.IsNotNull() || !lockAction.TransitionLockRequired) continue;
                
                dst.Add(lockAction);
            }
        }

        #region Debug (Editor Only)

#if UNITY_EDITOR

        // 용도: 에디터 설정 조합 사전 검증 훅
        // -> transitionLockRequired 설정 누락 위험 조기 경고 목적
        protected virtual void OnValidate()
        {
            int lockActionCount =
                CountTransitionLockActions(onEnterActions) +
                CountTransitionLockActions(updateActions) +
                CountTransitionLockActions(onExitActions);

            // lockRequired == true 인데 상태 전환 지연이 필요한 행동이 없는 경우
            // 무한 대기가 발생할 수 있으므로 예외 호출
            if (transitionLockRequired && lockActionCount == 0)
            {
                Logg.LogError($"[ActionStateSO] '{name}' has transitionLockRequired:true " +
                              $"but no action implements {nameof(IStateTransitionLock)})");
            }
        }

        // 용도: 액션 목록 잠금 구현 개수 집계 유틸리티
        // -> OnValidate 검증 지표 산출 목적
        private int CountTransitionLockActions(List<CharacterActionSO> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            int count = 0;
            foreach (var action in list)
            {
                if (action is IStateTransitionLock && action.IsNotNull())
                    count++;
            }

            return count;
        }
#endif

        #endregion
    }
}
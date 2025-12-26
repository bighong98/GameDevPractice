using System;
using System.Collections.Generic;
using TH.Control.Data;
using TH.Utils;
using UnityEngine;

namespace TH.Control.State
{
    public class ActionStateMachine : MonoBehaviour, IActionStateController
    {
        [SerializeField] private ActionStateSO initialState;
        [SerializeField] private List<ActionStateTransition> globalTransitions = new();
        
        public IActionState currentState;
        public IActionState remainState; // 상태를 유지할 때 사용하는 더미 상태

        [HideInInspector] public float stateTime;

        public ComponentProvider Components { get; private set; }

        private void Awake()
        {
            Components = new ComponentProvider(gameObject);
        }

        private void Start()
        {
            if (initialState == null) return;
            
            TransitionToState(initialState);
        }
        
        private void OnEnable()
        {
            if (currentState == null && initialState != null)
                TransitionToState(initialState);
        }

        private void OnDisable()
        {
            // 씬 언로드/비활성화 시 유령 전환 방지
            UnbindTransitions();
        }

        private void Update()
        {
            if (currentState == null) return;
            // 전역 상태 전환 조건 우선 검사
            // -> 만족하는 전환 조건이 있다면 CheckGlobalTransitionsPolling() 내부에서 즉시 상태 전환 실행
            if (CheckGlobalTransitionsPolling()) return;
            
            currentState.UpdateState(this);
            stateTime += Time.deltaTime;
        }

        #region IActionStateController

        public void TransitionToState(IActionState nextState, bool force = false)
        {
            // RemainState거나 null이면 전환하지 않음
            if (nextState == remainState || nextState == null) return;
            this.Log($"{gameObject.name}: {currentState} -> {nextState}");
            
            if (!force && _isLocked)
            {
                _pendingState = nextState;
                return;
            }
            
            // 기존 상태 event-driven 전환 조건 구독 해제 및 핸들러 정리
            UnbindTransitions();
            // 기존 상태 전환 잠금 이벤트 구독 해제 및 핸들러 정리
            DisposeTransitionLockHandler();
            
            // 기존 상태 퇴장 로직 실행
            if (currentState.IsNotNull())
                currentState.ExitState(this);
            
            // 상태 전환
            currentState = nextState;
            stateTime = 0;
            
            _pendingState = null;
            _isLocked = currentState.TransitionLockRequired;
            if (_isLocked)
            {
                _transitionUnlockHandler 
                    = currentState.BindTransitionUnlock(this, OnUnlockTransition);
            }
            
            // 새 상태 진입 로직 실행
            if (currentState.IsNotNull())
                currentState.EnterState(this);
            // 새 상태 event-driven 전환 조건 구독
            BindTransitions();
            
            this.Log($"[{gameObject.name}] TransitionToState({nextState.GetType().Name})"
                , Logg.LoggingMode.InProgress);
        }

        #endregion

        #region Transition Event Handle

        // 상태 전환 조건 감지 이벤트 구독 해제용(or 내부 클로저 정리) 핸들러 목록
        // -> event-driven 상태 전환 구현 목적
        // -> 상태 전환 시 기존 이벤트의 핸들러는 .Dispose()로 이벤트 구독해제 및 핸들러 정리
        // 현시점 구현상 구독할 이벤트가 없으면 빈 객체 (StateConditionHandler.Empty) 반환함 
        private readonly List<IDisposable> _transitionHandlers = new();
        private void BindTransitions()
        {
            if (currentState is not {} state || !state.IsNotNull()) return;
            
            // 글로벌 전환 조건 감지 이벤트 구독
            BindTransitionList(globalTransitions);
            
            // 현재 상태 객체에 전환 조건 감지 이벤트 구독
            state.BindTransitions(
                controller: this,
                register: sub =>
                {
                    // 구독할 이벤트가 있다면 이벤트 해제용 핸들러 캐싱
                    if (sub != null) _transitionHandlers.Add(sub);
                }
            );
        }

        // 기존 상태 전환 조건 감지 이벤트 구독 일괄 해제
        private void UnbindTransitions()
        {
            foreach (var handler in _transitionHandlers)
            {
                try { handler?.Dispose(); }
                catch (Exception e) { Logg.LogError($"[{gameObject.name}.UnbindTransitions()] - {e}"); }
            }

            _transitionHandlers.Clear();
        }
        
        // 글로벌 상태 전환 대응용
        
        private void BindTransitionList(List<ActionStateTransition> list)
        {
            if (list == null || list.Count == 0) return;

            foreach (var t in list)
            {
                var destination = t.DestinationState;
                if (destination == null) continue;

                if (t.Condition is not {} condition) continue;

                var token = condition.Bind(
                    controller: this,
                    onTriggered: () => TransitionToState(destination)
                );

                if (token != null)
                    _transitionHandlers.Add(token);
            }
        }

        #endregion

        #region Global Transition Handle

        private bool CheckGlobalTransitionsPolling()
        {
            if (globalTransitions == null || globalTransitions.Count == 0)
                return false;

            foreach (var t in globalTransitions)
            {
                // Transition 구조체 내부 참조 유효성 검사
                if (t is not { DestinationState: { } dest, Condition: { } cond }
                    || !dest.IsNotNull() || !cond.IsNotNull())
                    continue;
                // 조건 검사
                if (!cond.Decide(this)) continue;
                // 동일한 상태로의 전환인지 확인 + 동일 상태로의 전환 허락 여부 확인
                if (currentState == dest && !dest.AllowSelfTransition) continue;
                
                // 상태 전환 및 루프 종료
                // 글로벌 상태 전환 조건은 전환 지연(lock)을 무시함
                TransitionToState(dest, force: true);
                return true; 
            }

            return false;
        }

        #endregion

        #region Transition Lock Handle

        private bool _isLocked;
        private IActionState _pendingState;
        
        // 상태 전환 잠금 해제 이벤트를 구독 해제하기 위한 핸들러
        private IDisposable _transitionUnlockHandler;

        // 이전 상태의 Ready 핸들러 해제
        private void DisposeTransitionLockHandler()
        {
            _transitionUnlockHandler?.Dispose(); 
            _transitionUnlockHandler = null;
        }
        
        private void OnUnlockTransition()
        {
            if (!_isLocked) return;
            _isLocked = false;
            
            if (_pendingState == null) return;
            
            // 대기 중인 전환이 있다면 수행
            var next = _pendingState;
            _pendingState = null;
            TransitionToState(next);
        }
        

        #endregion
    }
}


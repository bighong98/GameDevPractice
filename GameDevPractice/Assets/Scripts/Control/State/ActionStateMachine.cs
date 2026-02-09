using System;
using System.Collections.Generic;
using System.Threading;
using TH.Control.Data;
using TH.Utils;
using UnityEngine;

namespace TH.Control.State
{
    public class ActionStateMachine : MonoBehaviour, IActionStateController
    {
        [SerializeField] private ActionStateSO initialState;
        [SerializeField] private List<ActionStateTransition> globalTransitions = new();
        
        private IActionState currentState;
        public IActionState remainState; // 상태를 유지할 때 사용하는 더미 상태

        [HideInInspector] public float stateTime;

        public ComponentProvider Components { get; private set; }
        public CancellationToken StateToken { get; private set; }
        public bool IsTransitionLocked => _transitionLockCount > 0;

        private int _transitionLockCount;
        private IActionState _pendingState;
        private IDisposable _transitionUnlockHandler;
        private bool _stateEntryLockActive;
        private int _nextRuntimeLockId = 1;
        private readonly HashSet<int> _runtimeLockIds = new();

        private void Awake()
        {
            Components = new ComponentProvider(gameObject);
            RenewStateToken();
        }

        private void Start()
        {
            if (initialState == null) return;
            
            TransitionToState(initialState);
        }
        
        private void OnEnable()
        {
            if (_stateTokenSource == null)
                RenewStateToken();
            
            if (currentState == null && initialState != null)
                TransitionToState(initialState);
        }

        private void OnDisable()
        {
            // 씬 언로드/비활성화 시 유령 전환 방지
            UnbindTransitions();
            ResetTransitionLocksOnStateChange();
            TryCancelDisposeStateToken();
        }

        private void OnDestroy()
        {
            TryCancelDisposeStateToken();
        }

        private void Update()
        {
            if (currentState == null) return;
            // 전역 상태 전환 조건 우선 검사
            // -> 만족하는 전환 조건이 있다면 CheckGlobalTransitionsPolling() 내부에서 즉시 상태 전환 실행
            if (CheckGlobalPollingConditions()) return;
            // 이벤트에 의해 추가 확인이 필요한 조건 검사
            if (CheckArmedTransitionsPolling()) return;
            
            currentState.UpdateState(this);
            stateTime += Time.deltaTime;
        }

        #region IActionStateController

        public void TransitionToState(IActionState nextState, bool ignoreLock = false)
        {
            if (nextState == remainState || nextState == null) return;

            if (nextState is InitialStateSO)
            {
                if (initialState == null)
                {
                    Logg.LogError($"[{gameObject.name}] TransitionToState - initialState and nextState is invalid", this);
                    return;
                }

                TransitionToState(initialState, ignoreLock);
                return;
            }

            this.Log($"{gameObject.name}: {currentState} -> {nextState}");

            if (!ignoreLock && IsTransitionLocked)
            {
                this.Log($"[{gameObject.name}] TransitionToState() - new pendingState updated: ({nextState})", Logg.LoggingMode.Completed);
                _pendingState = nextState;
                return;
            }

            RenewStateToken();

            _stateArmed.Clear();
            _pendingState = null;
            UnbindTransitions();
            ResetTransitionLocksOnStateChange();

            if (currentState.IsNotNull())
                currentState.ExitState(this);

#if UNITY_EDITOR
            var prevState = currentState;
#endif
            currentState = nextState;
            stateTime = 0f;
#if UNITY_EDITOR
            this.Log($"[{gameObject.name}] TransitionToState({prevState?.GetType().Name} -> {nextState.GetType().Name})",
                Logg.LoggingMode.Completed);
#endif
            if (currentState.IsNotNull())
                currentState.EnterState(this);

            BeginStateEntryLock(currentState);
            BindTransitions();
        }

        public IDisposable AcquireTransitionLock(object owner = null)
        {
            int lockId = _nextRuntimeLockId++;
            _runtimeLockIds.Add(lockId);
            _transitionLockCount++;

            return new DisposableDelegate(() => ReleaseRuntimeTransitionLock(lockId));
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
                // condition null-check
                if (t.Condition is not {} condition) continue;
                if (!condition.IsNotNull()) continue;
                // Polling 타입은 이벤트 미지원이므로 스킵
                if (condition.Measure == StateConditionMeasures.Polling) continue;
                
                var token = condition.Bind(
                    controller: this,
                    onTriggered: () => HandleConditionTriggered(
                        condition: condition,
                        destination: destination,
                        isGlobal: true,
                        ignoreForce: true) // 글로벌은 항상 lock 무시
                );

                if (token != null)
                    _transitionHandlers.Add(token);
            }
        }

        #endregion

        #region Global Transition Handle

        private bool CheckGlobalPollingConditions()
        {
            if (globalTransitions == null || globalTransitions.Count == 0)
                return false;

            foreach (var t in globalTransitions)
            {
                // ActionStateTransition 구조체 내부 참조 유효성 검사
                if (t is not { DestinationState: { } dest, 
                                Condition: { } cond } ) continue;
                if (!dest.IsNotNull() || !cond.IsNotNull()) continue;
                // Polling 타입 외에는 프레임 단위 검사x
                if (cond.Measure != StateConditionMeasures.Polling) continue;
                // 조건 평가 
                if (!cond.Decide(this)) continue;
                // 동일한 상태로의 전환인지 확인 + 동일 상태로의 전환 허락 여부 확인
                if (currentState == dest && !dest.AllowSelfTransition) continue;
                
                // 상태 전환 및 루프 종료
                // force: true -> 글로벌 상태 전환 조건은 lock 무시
                TransitionToState(dest, ignoreLock: true);
                return true; 
            }

            return false;
        }

        #endregion

        #region Transition Lock Handle

        private void BeginStateEntryLock(IActionState state)
        {
            if (state is null || !state.TransitionLockRequired) return;

            _stateEntryLockActive = true;
            _transitionLockCount++;
            _transitionUnlockHandler = state.BindTransitionUnlock(this, OnUnlockTransition);
        }

        private void ResetTransitionLocksOnStateChange()
        {
            _transitionUnlockHandler?.Dispose();
            _transitionUnlockHandler = null;
            _stateEntryLockActive = false;
            _runtimeLockIds.Clear();
            _transitionLockCount = 0;
        }

        private void ReleaseRuntimeTransitionLock(int lockId)
        {
            if (!_runtimeLockIds.Remove(lockId)) return;
            ReleaseTransitionLock();
        }

        private void OnUnlockTransition()
        {
            if (!_stateEntryLockActive) return;

            _stateEntryLockActive = false;
            ReleaseTransitionLock();
        }

        private void ReleaseTransitionLock()
        {
            if (_transitionLockCount <= 0) return;

            _transitionLockCount--;
            if (_transitionLockCount > 0 || _pendingState is not { } nextState) return;

            _pendingState = null;
            TransitionToState(nextState);
        }
        

        #endregion
        
        private struct ArmedTransition
        {
            public IActionStateCondition Condition;
            public IActionState Destination;
            public bool IgnoreLock;
        }

        // 상태 전이용 (현재 state에 종속)
        private readonly List<ArmedTransition> _stateArmed = new();

        // 글로벌 전이용
        private readonly List<ArmedTransition> _globalArmed = new();
        
        public void HandleConditionTriggered(
            IActionStateCondition condition,
            IActionState destination,
            bool isGlobal,
            bool ignoreForce = false)
        {
            if (condition == null || destination == null) return;

            switch (condition.Measure)
            {
                case StateConditionMeasures.EventDriven:
                    TransitionToState(destination, ignoreForce);
                    break;

                case StateConditionMeasures.EventTriggeredPolling:
                    if (condition.Decide(this))
                        TransitionToState(destination, ignoreForce);
                    break;

                case StateConditionMeasures.Both:
                {
                    // armed 등록 (중복 방지)
                    var list = isGlobal ? _globalArmed : _stateArmed;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (ReferenceEquals(list[i].Condition, condition) &&
                            ReferenceEquals(list[i].Destination, destination))
                        {
                            // 이미 등록됨 -> force만 갱신하고 종료
                            var at = list[i];
                            at.IgnoreLock |= ignoreForce;
                            list[i] = at;
                            goto CHECK_IMMEDIATE;
                        }
                    }

                    list.Add(new ArmedTransition
                    {
                        Condition = condition,
                        Destination = destination,
                        IgnoreLock = ignoreForce
                    });

                    CHECK_IMMEDIATE:
                    // 트리거 호춸 직후 즉시 1회 Decide 검사
                    if (condition.Decide(this))
                        TransitionToState(destination, ignoreForce);

                    break;
                }

                case StateConditionMeasures.Polling:
                default:
                    // Polling-only는 여기로 들어올 일이 없어야 정상 (Bind 안 하므로)
                    break;
            }
        }
        
        private bool CheckArmedTransitionsPolling()
        {
            // 글로벌 우선
            // 같은 분류 안에서도 하위 상태 전환 조건(StateConditionSO.conditions) 목록 내부 순서에 의해 우선순위 적용됨
            return TryConsumeGlobalArmed(_globalArmed) || 
                   TryConsumeArmed(_stateArmed);
        }

        private bool TryConsumeArmed(List<ArmedTransition> list)
        {
            if (list.Count == 0) return false;

            foreach (var t in list)
            {
                if (t.Condition == null || t.Destination == null) continue;
                if (!t.Condition.Decide(this)) continue;

                // 조건 만족 시 상태 전환 및 루프 종료
                TransitionToState(t.Destination, t.IgnoreLock);
                return true;
            }

            return false;
        }
        
        private bool TryConsumeGlobalArmed(List<ArmedTransition> list)
        {
            if (list.Count == 0) return false;

            foreach (var t in list)
            {
                if (t.Condition is not { } condition || !condition.IsNotNull()) continue;
                if (t.Destination is not { } dest|| !dest.IsNotNull()) continue;
                if (dest == currentState && !currentState.AllowSelfTransition) continue;
                
                if (!t.Condition.Decide(this)) continue;
                
                // 조건 만족 시 상태 전환 및 루프 종료
                TransitionToState(t.Destination, t.IgnoreLock);
                return true;
            }

            return false;
        }

        #region Handle State CTS
        
        private CancellationTokenSource _stateTokenSource;

        private void RenewStateToken()
        {
            // 이전 토큰은 Cancel, Dispose
            TryCancelDisposeStateToken();
            _stateTokenSource = new CancellationTokenSource();
            StateToken = _stateTokenSource.Token;
        }

        private void TryCancelDisposeStateToken()
        {
            if (_stateTokenSource == null) return;

            try { _stateTokenSource.Cancel(); }
            catch { /* ignore */ }
            finally
            {
                _stateTokenSource.Dispose();
                _stateTokenSource = null;
            }
        }

        #endregion

    }
}


using System;
using System.Collections.Generic;
using System.Threading;
using TH.Control.Data;
using TH.Utils;
using UnityEngine;

namespace TH.Control.State
{
    // 액션 상태 전이 실행과 전환 락/대기 큐를 조율하는 상태머신 컴포넌트
    // -> 이벤트 기반 전이와 Polling 전이 우선순위 제어 목적
    public class ActionStateMachine : MonoBehaviour, IActionStateController
    {
        // 상태머신 시작 시 최초 진입 기준 상태 에셋 참조
        [SerializeField] private ActionStateSO initialState;
        // 모든 상태에서 공통으로 평가하는 전역 전이 목록
        [SerializeField] private List<ActionStateTransition> globalTransitions = new();
#if UNITY_EDITOR
        [Header("Debug")]
        // 상태 전이 로그 출력 여부 제어 플래그
        [SerializeField] private bool logStateTransition;
#endif
        
        // 현재 활성 상태 인스턴스 참조
        private IActionState currentState;
        public IActionState remainState; // 상태를 유지할 때 사용하는 더미 상태

        // 현재 상태 체류 시간 누적값
        [HideInInspector] public float stateTime;

        // 상태 로직에서 공통 컴포넌트 조회에 사용하는 제공자
        public ComponentProvider Components { get; private set; }
        // 현재 상태 생명주기를 대표하는 취소 토큰
        public CancellationToken StateToken { get; private set; }
        // 전이 락 보유 여부 노출 프로퍼티
        public bool IsTransitionLocked => _transitionLockCount > 0;

        // 상태 전이 금지 구간 카운팅 값
        private int _transitionLockCount;
        // 락 해제 후 적용할 대기 전이 상태
        private IActionState _pendingState;
        // 상태 진입 락 해제 이벤트 구독 핸들
        private IDisposable _transitionUnlockHandler;
        // 상태 진입 기반 락 활성 여부
        private bool _stateEntryLockActive;
        // 런타임 전이 락 식별자 발급 시퀀스
        private int _nextRuntimeLockId = 1;
        // 현재 활성 런타임 전이 락 식별자 집합
        private readonly HashSet<int> _runtimeLockIds = new();

        // 컴포넌트 제공자 준비와 초기 상태 토큰 생성
        private void Awake()
        {
            Components = new ComponentProvider(gameObject);
            RenewStateToken();
        }

        // 초기 상태 지정 시 첫 상태 전이 실행
        private void Start()
        {
            if (initialState == null) return;
            
            TransitionToState(initialState);
        }
        
        // 재활성화 시 초기 상태 재진입을 위한 토큰/전이 데이터 재정렬
        private void OnEnable()
        {
            if (_stateTokenSource == null)
                RenewStateToken();

            if (initialState == null) return;

            // 상태머신을 초기 상태로 재진입
            // -> 풀 재사용 객체 이벤트 전이 정상화 목적
            if (currentState != null)
            {
                UnbindTransitions();
                _pendingState = null;
                _stateArmed.Clear();
                _globalArmed.Clear();
                ResetTransitionLocksOnStateChange();
                currentState = null;
            }

            TransitionToState(initialState, ignoreLock: true);
        }

        // 비활성화 시 전이 구독/락/토큰 정리
        private void OnDisable()
        {
            // 씬 언로드/비활성화 시 유령 전환 방지
            UnbindTransitions();
            ResetTransitionLocksOnStateChange();
            TryCancelDisposeStateToken();
        }

        // 파괴 시점 상태 토큰 최종 정리
        private void OnDestroy()
        {
            TryCancelDisposeStateToken();
        }

        // 프레임 단위 전이 검사와 현재 상태 업데이트 실행
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

        // 상태 전이 요청 검증과 실제 전환 수행
        // -> 락 활성 시 대기 상태 적재 후 락 해제 시점 재진입
        public void TransitionToState(IActionState nextState, bool ignoreLock = false)
        {
            if (nextState == remainState || nextState == null) return;

            if (currentState == nextState && currentState != null && !currentState.AllowSelfTransition)
            {
                return;
            }

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

            if (!ignoreLock && IsTransitionLocked)
            {
#if UNITY_EDITOR
                if (logStateTransition)
                {
                    this.Log($"[{gameObject.name}] TransitionToState() - new pendingState updated: ({nextState})", Logg.LoggingMode.InProgress);
                }
#endif
                _pendingState = nextState;
                return;
            }

            RenewStateToken();

            _stateArmed.Clear();
            _pendingState = null;
            UnbindTransitions();
            ResetTransitionLocksOnStateChange();

#if UNITY_EDITOR
            var prevState = currentState;
#endif
            if (currentState.IsNotNull())
                currentState.ExitState(this);

            currentState = nextState;
            stateTime = 0f;

#if UNITY_EDITOR
            if (logStateTransition)
            {
                this.Log($"[{gameObject.name}] TransitionToState({prevState?.GetType().Name} " + 
                        $"-> {nextState.GetType().Name})", Logg.LoggingMode.InProgress);
            }
#endif
            if (currentState.IsNotNull())
                currentState.EnterState(this);

            BeginStateEntryLock(currentState);
            BindTransitions();
        }

        // 외부 시스템용 런타임 전이 락 획득 API
        // -> 반환 IDisposable 해제 시 대응 락 카운트 감소
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
        // 현재 상태 + 전역 전이의 이벤트 기반 조건 구독 등록
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

        // 전역 Polling 조건 순회 후 최초 만족 전이 적용
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

        // 상태 진입 시 요구되는 전이 락 시작점
        // -> 상태가 unlock 콜백을 제공할 때까지 전이 보류 유지
        private void BeginStateEntryLock(IActionState state)
        {
            if (state is null || !state.TransitionLockRequired) return;

            _stateEntryLockActive = true;
            _transitionLockCount++;
            _transitionUnlockHandler = state.BindTransitionUnlock(this, OnUnlockTransition);
        }

        // 상태 변경 시점 전이 락 관련 런타임 데이터 초기화
        private void ResetTransitionLocksOnStateChange()
        {
            _transitionUnlockHandler?.Dispose();
            _transitionUnlockHandler = null;
            _stateEntryLockActive = false;
            _runtimeLockIds.Clear();
            _transitionLockCount = 0;
        }

        // 런타임 락 식별자 단위 해제 진입점
        private void ReleaseRuntimeTransitionLock(int lockId)
        {
            if (!_runtimeLockIds.Remove(lockId)) return;
            ReleaseTransitionLock();
        }

        // 상태 진입 락 해제 콜백 수신 핸들러
        private void OnUnlockTransition()
        {
            if (!_stateEntryLockActive) return;

            _stateEntryLockActive = false;
            ReleaseTransitionLock();
        }

        // 전이 락 카운트 감소와 대기 전이 재개
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
            // 트리거 발생 조건 참조
            public IActionStateCondition Condition;
            // 조건 만족 시 이동 대상 상태
            public IActionState Destination;
            // 전이 시 락 무시 여부
            public bool IgnoreLock;
        }

        // 상태 전이용 (현재 state에 종속)
        private readonly List<ArmedTransition> _stateArmed = new();

        // 글로벌 전이용
        private readonly List<ArmedTransition> _globalArmed = new();
        
        // 조건 트리거 측 이벤트 수신 시 전이 전략 분기 실행
        // -> 조건 측정 방식(EventDriven/Polling/Both)에 맞춰 즉시 전이 또는 armed 큐 적재
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
        
        // armed 큐 Polling 평가 진입점
        // -> 전역 armed를 먼저 소비해 우선순위 보장
        private bool CheckArmedTransitionsPolling()
        {
            // 글로벌 우선
            // 같은 분류 안에서도 하위 상태 전환 조건(StateConditionSO.conditions) 목록 내부 순서에 의해 우선순위 적용됨
            return TryConsumeGlobalArmed(_globalArmed) || 
                   TryConsumeArmed(_stateArmed);
        }

        // 일반 armed 목록 순회 후 만족 조건 1건 소비
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
        
        // 전역 armed 목록 순회 후 만족 조건 1건 소비
        // -> self transition 불가 정책을 먼저 검증
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
        
        // 현재 상태 토큰 소스 보관 필드
        private CancellationTokenSource _stateTokenSource;

        // 상태 전이 단위 취소 토큰 갱신
        private void RenewStateToken()
        {
            // 이전 토큰은 Cancel, Dispose
            TryCancelDisposeStateToken();
            _stateTokenSource = new CancellationTokenSource();
            StateToken = _stateTokenSource.Token;
        }

        // 상태 토큰 소스 안전 취소 및 해제
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


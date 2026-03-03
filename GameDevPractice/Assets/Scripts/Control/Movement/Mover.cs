using System;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Attribute.Data;
using TH.Attribute.Stat;
using TH.Core.Service;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting;

namespace TH.Control.Movement
{
    // 이동 컴포넌트 저장 데이터 패키징 구조체
    [Preserve][Serializable]
    public struct MoverSaveData
    {
        // 월드 좌표 저장 필드
        public SerializableVector3 position;
        // 오일러 회전값 저장 필드
        public SerializableVector3 rotation;
    }
    
    // 네비게이션 이동 제어 및 이동 상태 저장 복원 담당 컴포넌트
    public class Mover : MonoBehaviour, ISavable, IMover
    {
        // 이동속도 스탯 참조 SO

        [SerializeField] private AssetReferenceGameStatSO moveSpeedStatReference;
        [NonSerialized] private GameStatSO moveSpeedStatSO;
        // 기본 걷기 속도 값
        [SerializeField] private float walkSpeed = 2f;
        // 기본 달리기 속도 값
        [SerializeField] private float runSpeed = 6f;
        // 최종 속도 보정 배율
        [SerializeField] private float speedFraction = 1f;
        
        // NavMesh 기반 경로 이동 에이전트 참조
        private NavMeshAgent navMeshAgent;
        // 생존 상태 확인용 체력 컴포넌트 참조
        private Health health;
        // 스탯 바인딩 제공자 참조
        private IStatHolder statHolder;
        // 이동속도 스탯 런타임 캐시

        private bool isResolvingMoveSpeedStatReference;
        private IGameStat moveSpeedStat;
        
        // 목적지 설정 알림 이벤트
        public event Action OnDestinationSet;   
        // 목적지 도착 알림 이벤트
        public event Action OnArrived;
        // 추적 대상 변경 알림 이벤트
        public event Action<Transform> OnFollowingTargetSet;

        // 현재 추적 대상 조회 프로퍼티
        public Transform FollowingTarget => followingTarget;

        
        // 현재 추적 중인 대상 트랜스폼 참조
        private Transform followingTarget;
        // 현재 이동 목적지 캐시 좌표
        private Vector3 currentDestination = Vector3.zero;
        // 도착 판정 거리 임계값
        private const float distanceTolerance = 2.0f;
        // 거리 비교 안정화 버퍼값
        
        private static bool CanControlAgent(NavMeshAgent agent)
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }
        private const float distanceCompareBuffer = 0.1f;


        // 필수 컴포넌트 캐시 및 스탯 SO 유효성 점검 단계
        private void Awake()
        {
            TryGetComponent(out navMeshAgent);
            TryGetComponent(out health);
            TryGetComponent(out statHolder);

            if ((moveSpeedStatSO.IsNull() || moveSpeedStatSO.LegacyId == default) &&
                (moveSpeedStatReference == null || !moveSpeedStatReference.RuntimeKeyIsValid()))
            {
                this.LogWarning($"[{gameObject.name}.{GetType().Name}] invalid moveSpeedStatSO and moveSpeedStatReference", context: this);
            }

            EnsureMoveSpeedStatAsync().Forget();
        }

        // 이동속도 스탯 바인딩 활성화 단계
        private void OnEnable()
        {

            EnsureMoveSpeedStatAsync().Forget();
            SyncMoveSpeedStat();
        }

        // 이동속도 스탯 바인딩 해제 단계
        private void OnDisable()
        {
            UnSyncMoveSpeedStat();
        }

        // 도착 여부 감시 루프
        private void Update()
        {
            // 사망 상태 조기 종료 가드
            if (health.IsDead) return;
            // 목적지 미설정 상태 조기 종료 가드

            if (moveSpeedStat == null)
            {
                SyncMoveSpeedStat();
                EnsureMoveSpeedStatAsync().Forget();
            }
            if (currentDestination == Vector3.zero) return;
            
            float distanceToWaypoint = Vector3.SqrMagnitude(transform.position - currentDestination);
            if (distanceToWaypoint < distanceTolerance)
            {
                OnArrived?.Invoke();
            }
        }

        // 월드 좌표 목적지 설정 처리
        public void SetDestination(Vector3 destination, bool notify = true)
        {
            if (destination == Vector3.zero) return;

            currentDestination = destination;
            if (notify) OnDestinationSet?.Invoke();
        }

        // 대상 기준 거리 유지 목적지 계산 처리
        public bool SetDestination(Transform target, float requiredDistance, bool notify = true)
        {
            // 대상 없음 조기 종료 가드
            if (target == null) return false;

            // 거리 입력 보정 및 제곱거리 비교 준비 단계
            float clampedRequiredDistance = Mathf.Max(0f, requiredDistance);
            float bufferedRequiredDistance = clampedRequiredDistance + distanceCompareBuffer;
            Vector3 toTarget = target.position - transform.position;
            float sqrRequiredDistance = bufferedRequiredDistance * bufferedRequiredDistance;

            if (toTarget.sqrMagnitude <= sqrRequiredDistance)
            {
                // 요구 거리 충족 시 이동 정지 상태 전환
                currentDestination = Vector3.zero;

                if (CanControlAgent(navMeshAgent))
                {
                    navMeshAgent.ResetPath();
                    navMeshAgent.isStopped = true;
                }

                return false;
            }

            // 대상 주변 유지거리 지점 계산 후 목적지 갱신
            Vector3 destination = target.position - toTarget.normalized * clampedRequiredDistance;
            SetDestination(destination, notify);
            return true;
        }

        // 대상 추적 시작 처리
        public void Follow(Transform target, bool stopIfInvalidTarget)
        {
            SetFollowingTarget(target);

            if (followingTarget == null)
            {
                // 유효 대상 부재 시 이동 상태 정리 분기
                if (stopIfInvalidTarget) ResetMovementState();
                return;
            }

            MoveTo(followingTarget.position, MoveType.Run, notify: false);
        }

        // 추적 대상 변경 및 알림 이벤트 발행 처리
        private void SetFollowingTarget(Transform target)
        {
            // 동일 대상 재지정 방지 가드
            if (target != null && ReferenceEquals(followingTarget, target)) return;

            followingTarget = target;
            OnFollowingTargetSet?.Invoke(followingTarget);
        }

        // 현재 목적지를 향한 네비게이션 이동 실행
        public void Move(MoveType moveType = MoveType.Run)
        {
            // 목적지 미설정 상태 조기 종료 가드
            if (currentDestination == Vector3.zero) return;

            navMeshAgent.destination = currentDestination;
            navMeshAgent.speed = (moveType == MoveType.Run ? runSpeed : walkSpeed) * Mathf.Clamp01(speedFraction);
            navMeshAgent.isStopped = false;
        }

        // 네비게이션 이동 정지 처리
        public void Stop()
        {
            navMeshAgent.isStopped = true;
        }

        // 목적지 및 추적 대상 초기화 후 에이전트 정지 처리
        public void ResetMovementState()
        {
            currentDestination = Vector3.zero;
            SetFollowingTarget(null);

            if (!CanControlAgent(navMeshAgent)) return;

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }

        // 목적지 설정과 이동 실행 결합 편의 메서드
        public void MoveTo(Vector3 destination, MoveType moveType = MoveType.Run, bool notify = true)
        {
            SetDestination(destination, notify);
            Move(moveType);
        }
        
        private async UniTaskVoid EnsureMoveSpeedStatAsync()
        {
            if (moveSpeedStatSO.IsNotNull() && moveSpeedStatSO.LegacyId != default)
            {
                return;
            }

            if (isResolvingMoveSpeedStatReference)
            {
                return;
            }

            if (moveSpeedStatReference == null || !moveSpeedStatReference.RuntimeKeyIsValid())
            {
                return;
            }

            isResolvingMoveSpeedStatReference = true;
            try
            {
                var loadedStat = await ResourceManager.Instance.ExtractAssetRefAsync<GameStatSO>(
                    moveSpeedStatReference,
                    this.GetCancellationTokenOnDestroy());

                if (loadedStat.IsNull())
                {
                    return;
                }

                moveSpeedStatSO = loadedStat;
                if (isActiveAndEnabled)
                {
                    SyncMoveSpeedStat();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                isResolvingMoveSpeedStatReference = false;
            }
        }

        // 이동속도 스탯 바인딩 및 초기 반영 처리
        private void SyncMoveSpeedStat()
        {
            if (moveSpeedStat != null) return;
            if (statHolder.IsNull()) return;
            if (moveSpeedStatSO.IsNull() || moveSpeedStatSO.LegacyId == default) return;

            if (statHolder.BindEvent(moveSpeedStatSO, OnMoveSpeedStatDirty) is { } bindResult)
            {
                moveSpeedStat = bindResult;
                OnMoveSpeedStatDirty();
            }
        }

        // 이동속도 스탯 이벤트 바인딩 해제 처리
        private void UnSyncMoveSpeedStat()
        {
            if (statHolder.IsNull() || moveSpeedStatSO.IsNull()) return;

            statHolder.UnBindEvent(moveSpeedStatSO, OnMoveSpeedStatDirty);
            moveSpeedStat = null;
        }

        // 이동속도 스탯 변경 반영 콜백
        private void OnMoveSpeedStatDirty()
        {
            if (statHolder == null) return;
            if (moveSpeedStat == null && !statHolder.TryGetStat(moveSpeedStatSO, out moveSpeedStat))
            {
                this.LogWarning($"OnMoveSpeedStatDirty() - failed to get move speed stat from statholder. moveSpeedStatSO: {moveSpeedStatSO}", context: this);
                return;
            }

            walkSpeed = moveSpeedStat.Value;
            runSpeed = moveSpeedStat.Value * 2f;
        }

        #region ISavable
        
        // 이동 저장 데이터 생성 
        public object CaptureState()
        {
            MoverSaveData data = new MoverSaveData
            {
                position = new SerializableVector3(transform.position),
                rotation = new SerializableVector3(transform.eulerAngles)
            };

            return data;
        }

        // 이동 저장 데이터 복원
        public bool RestoreState(object state)
        {
            if (state is not MoverSaveData data) return false;
            if (!TryGetComponent(out navMeshAgent)) return false;

            Vector3 restoredPosition = data.position.ToVector();
            Vector3 restoredRotation = data.rotation.ToVector();

            currentDestination = Vector3.zero;
            SetFollowingTarget(null);

            bool wasAgentEnabled = navMeshAgent.enabled;
            bool canControlAgent = CanControlAgent(navMeshAgent);
            if (wasAgentEnabled)
            {
                if (canControlAgent)
                {
                    navMeshAgent.ResetPath();
                    navMeshAgent.velocity = Vector3.zero;
                    navMeshAgent.isStopped = true;
                }

                navMeshAgent.enabled = false;
            }

            this.Log($"({gameObject.name}) - set position: {restoredPosition}, set rotation: {restoredRotation}", Logg.LoggingMode.Completed);
            transform.position = restoredPosition;
            transform.eulerAngles = restoredRotation;

            if (wasAgentEnabled)
            {
                navMeshAgent.enabled = true;

                if (CanControlAgent(navMeshAgent))
                {
                    navMeshAgent.ResetPath();
                    navMeshAgent.velocity = Vector3.zero;
                    navMeshAgent.isStopped = true;
                }
            }

            return true;
        }

        // 이동 상태 기본값 리셋 처리
        public void ResetToDefaultState()
        {
            ResetMovementState();
        }
        #endregion
    }
}


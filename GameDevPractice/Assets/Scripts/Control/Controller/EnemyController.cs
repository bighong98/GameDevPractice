using System;
using TH.Attribute;
using TH.Combat;
using TH.Core.Service;
using TH.Utils;
using TH.Control.Movement;
using UnityEngine;

namespace TH.Control
{
    public class EnemyController : MonoBehaviour, ISightHandler
    {
        [SerializeField] private float chaseDistance = 50f;
        [SerializeField] private PatrolPath patrolPath; // need to connect by inspector
        [SerializeField] private float waypointTolerance = 2f;
        
        private IAttacker attacker;
        private ISkillController skillController;
        private Health health;
        private IMover mover;
        
        private IPlayerHolder playerHolder;
        private PlayerController player;
        
        private LazyValue<Vector3> guardPosition;
        
        private Health playerHealth;

        public float SightThreshold { get; private set; }
        private IGameScanner<Health> scanner;

        private readonly float cd;
        
        private void Awake()
        {
            TryGetComponent(out attacker);
            TryGetComponent(out skillController);
            TryGetComponent(out mover);
            TryGetComponent(out health);
            
            guardPosition = new LazyValue<Vector3>(GetDefaultGuardPosition);
            
            playerHolder = ServiceLocator.Get<IPlayerHolder>();
            
            // LayerMask mask =  LayerMask.GetMask("Character", "Ally");
            // scanner = new GameScanner<Health>(this, mask);

            SightThreshold = chaseDistance * chaseDistance; // 거리 비교에 SqrMagnitude 사용하기 때문에 제곱값 사용
        }
        
        private void Start()
        {
            if (playerHolder.GetPlayerInstance is PlayerController p)
            {
                player = p;
                player.TryGetComponent(out playerHealth);
            }
            
            guardPosition.ForceInit();
            GoToWayPoint();
        }

        private void Update()
        {
            if (health.IsDead) return;
            if (playerHealth == null || playerHealth.IsDead) return;
            
            bool isInSight = IsInSight;
            bool isTargetValid = attacker.IsTargetValid;
            
            // 타겟이 없는데 사거리 안에 들어온 경우 -> 타겟 설정
            if (!isTargetValid && isInSight)
            {
                attacker.SetTarget(playerHealth);
                skillController.TryRequestActiveSkill();
            }
            // 타겟이 있는데 사거리 밖으로 나간 경우 -> 타겟 해제
            else if (isTargetValid && !isInSight)
            {
                attacker.SetTarget(null);
            }
        }
        
        private void OnEnable()
        {
            if (playerHolder.GetPlayerInstance is PlayerController p)
                player = p;
            
            if (mover.IsNotNull())
                mover.OnArrived += SetNextDestination;
        }

        private void OnDisable()
        {
            if (mover.IsNotNull())
                mover.OnArrived -= SetNextDestination;
        }

        #region Patrol Behaviour (WayPoint)

        private int currentWaypointIndex = 0;
        
        private Vector3 GetDefaultGuardPosition()
        {
            return transform.position;
        }
        
        private void UpdateWayPoint()
        {
            if (patrolPath == null) return;
            currentWaypointIndex = patrolPath.GetNextIndex(currentWaypointIndex);
            guardPosition.Value = patrolPath.GetWaypoint(currentWaypointIndex);
        }

        private void GoToWayPoint()
        {
            mover.SetDestination(guardPosition.Value);
        }

        #endregion
        
        private void SetNextDestination()
        {
            if (IsInSight) return;
            if (patrolPath == null) return;
            
            float distanceToWaypoint = Vector3.SqrMagnitude(transform.position - guardPosition.Value);
            if (distanceToWaypoint < waypointTolerance)
            {
                UpdateWayPoint();
            }
            //todo: 현재 전투 중인지 확인 (우연히 싸우다가 waypoint에 도착한 경우)
            GoToWayPoint();
        }

        private bool IsInSight => Vector3.SqrMagnitude(player.transform.position - transform.position) < SightThreshold;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() 
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, chaseDistance);
        }
#endif
        
    }
}


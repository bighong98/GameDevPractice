using System;
using System.Collections;
using System.Collections.Generic;
using GameDevTV.Utils;
using UnityEngine;
using RPG.Core;
using RPG.Attribute;
using RPG.Combat;
using RPG.Movement;

namespace RPG.Control
{
    public class AIController : MonoBehaviour
    {
        [SerializeField] private float chaseDistance = 5f;
        [SerializeField] private float suspicionTime = 3f;
        [SerializeField] private PatrolPath patrolPath; // need to connect by inspector
        [SerializeField] private float waypointTolerance = 2f;
        [SerializeField] private float waypointDwellTime = 3f;
        [SerializeField] private float patrolSpeedFraction = 0.2f;
        
        private Fighter fighter;
        private Health health;
        private Mover mover;
        private ActoinScheduler actionScheduler;
        
        private PlayerController player;

        private LazyValue<Vector3> guardPosition;
        private float timeSinceLastSawPlayer = Mathf.Infinity;
        private float timeSinceArrivedAtWaypoint = Mathf.Infinity;
        private int currentWaypointIndex = 0;

        private void Awake()
        {
            fighter = GetComponent<Fighter>();
            health = GetComponent<Health>();
            mover = GetComponent<Mover>();
            actionScheduler = GetComponent<ActoinScheduler>();
            guardPosition = new LazyValue<Vector3>(GetDefaultGuardPosition);
            
            if (GameObject.FindWithTag("Player") is { } foundPlayer)
            {
                player = foundPlayer.GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            if (health.IsDead) return; // 사망 상태라면 실행 취소
            if (IsInAttackRange && fighter.CanAttack(player.gameObject, out Health playerHealth))
            {
                timeSinceLastSawPlayer = 0;
                AttackBehaviour(playerHealth);
            }
            else if (timeSinceLastSawPlayer < suspicionTime)
            {
                SuspicionBehaviour();
            }
            else
            {
                PatrolBehaviour();
            }

            UpdateTimers();
        }

        private Vector3 GetDefaultGuardPosition()
        {
            return transform.position;
        }

        private void UpdateTimers()
        {
            timeSinceLastSawPlayer += Time.deltaTime;
            timeSinceArrivedAtWaypoint += Time.deltaTime;
        }

        private void PatrolBehaviour()
        {
            Vector3 nextPosition = guardPosition.value;
            if (patrolPath != null)
            {
                if (AtWaypoint())
                {
                    timeSinceArrivedAtWaypoint = 0;
                    CycleWaypoint();
                }

                nextPosition = GetCurrentWaypoint();
            }

            if (timeSinceArrivedAtWaypoint > waypointDwellTime)
            {
                mover.StartMoveAction(nextPosition, patrolSpeedFraction); // 범위 안에 플레이어가 없으면 본인 자리로 돌아감
            }
        }

        private bool AtWaypoint()
        {
            float distanceToWaypoint = Vector3.SqrMagnitude(transform.position - GetCurrentWaypoint());
            // Debug.Log($"distanceToWaypoint: {distanceToWaypoint}, AtWaypoint: {distanceToWaypoint < waypointTolerance}");
            return distanceToWaypoint < waypointTolerance;
        }

        private void CycleWaypoint()
        {
            currentWaypointIndex = patrolPath.GetNextIndex(currentWaypointIndex);
        }

        private Vector3 GetCurrentWaypoint()
        {
            return patrolPath.GetWaypoint(currentWaypointIndex);
        }

        private void SuspicionBehaviour()
        {
            actionScheduler.CancelCurrentAction();
        }

        private void AttackBehaviour(Health playerHealth)
        {
            fighter.Attack(playerHealth);
        }

        private bool IsInAttackRange => Vector3.Distance(player.transform.position, transform.position) < chaseDistance;

        private void OnDrawGizmosSelected() // method called by Unity
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, chaseDistance);
        }
    }
}


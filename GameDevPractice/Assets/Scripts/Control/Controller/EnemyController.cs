using System;
using System.Collections;
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
        [SerializeField] private PatrolBehaviourModule patrolModule = new();

        private IAttacker attacker;
        private ISkillController skillController;
        private Health health;
        private IMover mover;

        private IPlayerHolder playerHolder;
        private PlayerController player;

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

            patrolModule ??= new PatrolBehaviourModule();
            playerHolder = ServiceLocator.Get<IPlayerHolder>();

            // LayerMask mask =  LayerMask.GetMask("Character", "Ally");
            // scanner = new GameScanner<Health>(this, mask);

            SightThreshold = chaseDistance * chaseDistance;
        }

        private IEnumerator Start()
        {
            if (playerHolder.GetPlayerInstance is PlayerController p)
            {
                player = p;
                player.TryGetComponent(out playerHealth);
            }

            patrolModule.Initialize(transform.position);

            yield return null;
            patrolModule.RequestInitialDestination(transform.position);
        }

        private void Update()
        {
            if (health.IsDead) return;
            if (playerHealth == null || playerHealth.IsDead) return;

            bool isInSight = IsInSight;
            bool isTargetValid = attacker.IsTargetValid;

            if (!isTargetValid && isInSight)
            {
                attacker.SetTarget(playerHealth);
                skillController.TryRequestActiveSkill();
            }
            else if (isTargetValid && !isInSight)
            {
                attacker.SetTarget(null);
            }
        }

        private void OnEnable()
        {
            if (playerHolder.GetPlayerInstance is PlayerController p)
                player = p;

            patrolModule ??= new PatrolBehaviourModule();

            if (mover.IsNotNull())
                mover.OnArrived += HandleMoverArrived;

            patrolModule.OnPatrolDestinationRequested += HandlePatrolDestinationRequested;
        }

        private void OnDisable()
        {
            if (mover.IsNotNull())
                mover.OnArrived -= HandleMoverArrived;

            if (patrolModule != null)
                patrolModule.OnPatrolDestinationRequested -= HandlePatrolDestinationRequested;
        }

        private void HandleMoverArrived()
        {
            if (!CanPatrolNow()) return;

            patrolModule.HandleArrived(transform.position);
        }

        private void HandlePatrolDestinationRequested(Vector3 destination)
        {
            if (!CanPatrolNow()) return;
            if (mover.IsNull()) return;

            mover.SetDestination(destination);
        }

        #if UNITY_EDITOR
        public bool HasPatrolPathInEditor => patrolModule != null && patrolModule.HasPatrolPath;

        public bool TryRegeneratePatrolWaypointsInEditor()
        {
            patrolModule ??= new PatrolBehaviourModule();

            UnityEditor.Undo.RecordObject(this, "Regenerate Patrol Waypoints");
            bool generated = patrolModule.TryRegeneratePatrolPath(transform.position);

            if (!generated)
                return false;

            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

            return true;
        }

        [ContextMenu("Patrol/Regenerate Waypoints")]
        private void RegeneratePatrolWaypoints()
        {
            bool generated = TryRegeneratePatrolWaypointsInEditor();

            if (!generated)
            {
                Debug.LogWarning($"[{nameof(EnemyController)}] Failed to regenerate patrol waypoints on '{name}'.", this);
                return;
            }

            Debug.Log($"[{nameof(EnemyController)}] Regenerated patrol waypoints on '{name}'.", this);
        }
#endif


        private bool CanPatrolNow()
        {
            return health.IsNotNull() && !health.IsDead && !IsInSight;
        }

        private bool IsInSight => player != null &&
                                  Vector3.SqrMagnitude(player.transform.position - transform.position) < SightThreshold;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, chaseDistance);

            if (patrolModule == null || !patrolModule.HasPatrolPath)
                return;

            var waypoints = patrolModule.Waypoints;
            if (waypoints == null || waypoints.Count == 0)
                return;

            const float waypointGizmoRadius = 0.2f;
            Gizmos.color = Color.yellow;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 current = waypoints[i];
                Gizmos.DrawSphere(current, waypointGizmoRadius);

                if (waypoints.Count < 2)
                    continue;

                Vector3 next = waypoints[(i + 1) % waypoints.Count];
                Gizmos.DrawLine(current, next);
            }
        }
#endif
    }
}


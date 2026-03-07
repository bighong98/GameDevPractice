using System;
using System.Collections.Generic;

using UnityEngine;
using TH.Control.Movement;
using UnityEngine.AI;


namespace TH.Control
{
    [Serializable]
    public class PatrolBehaviourModule
    {
        [SerializeField] private PatrolPathProvider patrolPathProvider = new();
        [SerializeField] private float waypointTolerance = 2f;

        [Header("Auto Patrol Path")]
        [SerializeField] private bool autoGenerateWhenWaypointsEmpty = true;
        [SerializeField, Min(2)] private int autoWaypointCount = 4;
        [SerializeField, Min(0.5f)] private float autoWaypointRadius = 6f;
        [SerializeField, Min(0.25f)] private float navMeshSampleDistance = 4f;
        [SerializeField, Min(0.1f)] private float minWaypointDistance = 1.5f;
        [SerializeField, Range(0f, 30f)] private float autoAngleJitterDegrees = 8f;
        [SerializeField, Range(0f, 0.5f)] private float autoRadiusJitterRatio = 0.1f;


        private int currentWaypointIndex;
        private Vector3 guardPosition;

        public event Action<Vector3> OnPatrolDestinationRequested;

        public bool HasPatrolPath => patrolPathProvider != null && patrolPathProvider.HasWaypoints;
        public IReadOnlyList<Vector3> Waypoints => patrolPathProvider?.Waypoints;


        public void Initialize(Vector3 defaultGuardPosition)
        {
            patrolPathProvider ??= new PatrolPathProvider();
            currentWaypointIndex = 0;
            guardPosition = defaultGuardPosition;

            EnsurePatrolPath(defaultGuardPosition);
            SyncGuardPositionToPatrolPath(defaultGuardPosition);
        }

        public void RequestInitialDestination(Vector3 currentPosition)
        {
            EnsurePatrolPath(currentPosition);
            SyncGuardPositionToPatrolPath(currentPosition);
            if (!HasPatrolPath) return;

            float toleranceSqr = waypointTolerance * waypointTolerance;
            if (Vector3.SqrMagnitude(currentPosition - guardPosition) <= toleranceSqr)
            {
                UpdateWaypoint();
            }

            RequestCurrentDestination();
        }

        public void HandleArrived(Vector3 currentPosition)
        {
            EnsurePatrolPath(currentPosition);
            if (!HasPatrolPath) return;

            float distanceToWaypointSqr = Vector3.SqrMagnitude(currentPosition - guardPosition);
            float toleranceSqr = waypointTolerance * waypointTolerance;
            if (distanceToWaypointSqr <= toleranceSqr)
            {
                UpdateWaypoint();
            }

            RequestCurrentDestination();
        }

        public bool TryRegeneratePatrolPath(Vector3 origin)
        {
            patrolPathProvider ??= new PatrolPathProvider();

            if (!TryGeneratePatrolPath(origin, out var generatedWaypoints))
                return false;

            patrolPathProvider.SetWaypoints(generatedWaypoints);
            currentWaypointIndex = 0;
            guardPosition = patrolPathProvider.GetWaypoint(currentWaypointIndex);
            return true;
        }

        #region Initialize

        private void EnsurePatrolPath(Vector3 origin)
        {
            if (HasPatrolPath) return;
            if (!autoGenerateWhenWaypointsEmpty) return;

            if (TryGeneratePatrolPath(origin, out var generatedWaypoints))
            {
                patrolPathProvider.SetWaypoints(generatedWaypoints);
                currentWaypointIndex = 0;
                guardPosition = patrolPathProvider.GetWaypoint(currentWaypointIndex);
            }
        }

        #endregion

        private void UpdateWaypoint()
        {
            if (!HasPatrolPath) return;

            currentWaypointIndex = patrolPathProvider.GetNextIndex(currentWaypointIndex);
            guardPosition = patrolPathProvider.GetWaypoint(currentWaypointIndex);
        }

        private bool TryGeneratePatrolPath(Vector3 origin, out System.Collections.Generic.List<Vector3> generatedWaypoints)
        {
            generatedWaypoints = null;

            if (autoWaypointCount < 2) return false;
            if (!TryResolveStartPoint(origin, out var startPoint)) return false;

            int targetWaypointCount = Math.Max(2, autoWaypointCount);
            int candidateCount = Math.Max(targetWaypointCount * 4, targetWaypointCount + 2);
            float minWaypointDistanceSqr = minWaypointDistance * minWaypointDistance;
            float angleJitterRadians = autoAngleJitterDegrees * Mathf.Deg2Rad;

            var random = CreatePatrolRandom(origin);
            float startAngle = RandomRange(random, 0f, Mathf.PI * 2f);

            var waypoints = new System.Collections.Generic.List<Vector3>(targetWaypointCount)
            {
                startPoint
            };

            Vector3 previous = startPoint;

            for (int i = 0; i < candidateCount && waypoints.Count < targetWaypointCount; i++)
            {
                float t = i / (float)candidateCount;
                float angle = startAngle + t * Mathf.PI * 2f + RandomRange(random, -angleJitterRadians, angleJitterRadians);

                float radiusScale = 1f + RandomRange(random, -autoRadiusJitterRatio, autoRadiusJitterRatio);
                float candidateRadius = Mathf.Max(0.5f, autoWaypointRadius * radiusScale);

                Vector3 candidate = startPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * candidateRadius;

                if (!TrySampleOnNavMesh(candidate, out var sampledPoint))
                    continue;

                if ((sampledPoint - waypoints[waypoints.Count - 1]).sqrMagnitude < minWaypointDistanceSqr)
                    continue;

                if (!IsReachable(previous, sampledPoint))
                    continue;

                waypoints.Add(sampledPoint);
                previous = sampledPoint;
            }

            if (waypoints.Count < 2)
                return false;

            if (!IsReachable(waypoints[waypoints.Count - 1], waypoints[0]))
                return false;

            generatedWaypoints = waypoints;
            return true;
        }

        private bool TryResolveStartPoint(Vector3 origin, out Vector3 startPoint)
        {
            return TrySampleNearest(origin, out startPoint);
        }


        private bool TrySampleNearest(Vector3 origin, out Vector3 sampledPoint)
        {
            sampledPoint = default;

            float radius = Mathf.Max(0.1f, navMeshSampleDistance);
            float maxRadius = Mathf.Max(radius, autoWaypointRadius * 2f);

            while (radius <= maxRadius + 0.01f)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(origin, out var navMeshHit, radius, UnityEngine.AI.NavMesh.AllAreas))
                {
                    sampledPoint = navMeshHit.position;
                    return true;
                }

                radius *= 2f;
            }

            return false;
        }


        private static System.Random CreatePatrolRandom(Vector3 origin)
        {
            unchecked
            {
                int seed = Environment.TickCount ^ (origin.GetHashCode() * 397);
                return new System.Random(seed);
            }
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }




        private bool TrySampleOnNavMesh(Vector3 candidate, out Vector3 sampledPoint)
        {
            sampledPoint = default;

            if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var navMeshHit, navMeshSampleDistance, UnityEngine.AI.NavMesh.AllAreas))
                return false;

            sampledPoint = navMeshHit.position;
            return true;
        }

        private bool IsReachable(Vector3 from, Vector3 to)
        {
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(from, to, UnityEngine.AI.NavMesh.AllAreas, path))
                return false;

            return path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }

        private void RequestCurrentDestination()
        {
            OnPatrolDestinationRequested?.Invoke(guardPosition);
        }
    

        private void SyncGuardPositionToPatrolPath(Vector3 referencePosition)
        {
            if (!HasPatrolPath)
                return;

            currentWaypointIndex = FindClosestWaypointIndex(referencePosition);
            guardPosition = patrolPathProvider.GetWaypoint(currentWaypointIndex);
        }

        private int FindClosestWaypointIndex(Vector3 referencePosition)
        {
            if (!HasPatrolPath)
                return 0;

            var waypoints = patrolPathProvider.Waypoints;
            if (waypoints == null || waypoints.Count == 0)
                return 0;

            int closestIndex = 0;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                float distanceSqr = Vector3.SqrMagnitude(referencePosition - waypoints[i]);
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }
    }
}

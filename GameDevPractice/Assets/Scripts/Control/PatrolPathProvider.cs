using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Control.Movement
{
    [Serializable]
    public class PatrolPathProvider
    {
        [SerializeField] private List<Vector3> waypoints = new();

        public List<Vector3> Waypoints => waypoints;
        public bool HasWaypoints => waypoints != null && waypoints.Count > 0;

        public void SetWaypoints(List<Vector3> newWaypoints)
        {
            waypoints = newWaypoints ?? new List<Vector3>();
        }

        public Vector3 GetWaypoint(int i)
        {
            if (!HasWaypoints) return default;

            int normalizedIndex = NormalizeIndex(i);
            return waypoints[normalizedIndex];
        }

        public int GetNextIndex(int i)
        {
            if (!HasWaypoints) return 0;

            int normalizedIndex = NormalizeIndex(i);
            return (normalizedIndex + 1) % waypoints.Count;
        }

        private int NormalizeIndex(int i)
        {
            if (!HasWaypoints) return 0;

            int count = waypoints.Count;
            int normalized = i % count;
            if (normalized < 0)
                normalized += count;

            return normalized;
        }
    }
}


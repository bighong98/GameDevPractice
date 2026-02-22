using System;
using UnityEngine;

namespace TH.Control.Movement
{
    public interface IMover
    {
        event Action OnDestinationSet; // 목적지 설정 시
        event Action OnArrived; // 도착 시
        event Action<Transform> OnFollowingTargetSet;
        Transform FollowingTarget { get; }

        
        void MoveTo(Vector3 destination, MoveType moveType = MoveType.Run, bool notify = true);
        void SetDestination(Vector3 destination, bool notify = true);
        bool SetDestination(Transform target, float requiredDistance, bool notify = true);
        void Follow(Transform target);
        void Move(MoveType moveType = MoveType.Run);
        void Stop();
    }

    public enum MoveType
    {
        Walk,
        Run,
        Jump,
    }
}


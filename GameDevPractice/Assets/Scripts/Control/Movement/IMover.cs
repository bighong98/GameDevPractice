using System;
using UnityEngine;

namespace TH.Control.Movement
{
    public interface IMover
    {
        event Action OnDestinationSet; // 목적지 설정 시
        event Action OnArrived; // 도착 시
        
        void MoveTo(Vector3 destination, MoveType moveType = MoveType.Run, bool notify = true); // SetDestination + Move
        void SetDestination(Vector3 destination, bool notify = true); // 목적지 설정 -> OnDestinationSet 호출하도록 구현
        void Move(MoveType moveType = MoveType.Run); // (목적지가 있는 경우) 이동 시작 
        void Stop(); // 이동 중지
    }

    public enum MoveType
    {
        Walk,
        Run,
        Jump,
    }
}


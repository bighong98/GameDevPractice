using System;
using TH.Control.Data;
using UnityEngine;

namespace TH.Control.State
{
    [Serializable]
    // 상태 전이 조건과 목적 상태를 묶는 전이 데이터 구조체
    public struct ActionStateTransition
    {
        public IActionStateCondition Condition => condition; // 조건
        public IActionState DestinationState => destinationState;        // 참일 때 갈 상태
        
        [SerializeField] private ActionStateConditionSO condition;
        [SerializeField] private ActionStateSO destinationState;
    }
}


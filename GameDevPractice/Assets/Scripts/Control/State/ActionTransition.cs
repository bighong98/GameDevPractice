using System;
using TH.Control.Data;
using UnityEngine;

namespace TH.Control.State
{
    [Serializable]
    public struct ActionStateTransition
    {
        public IActionStateCondition Condition => condition; // 조건
        public IActionState DestinationState => destinationState;        // 참일 때 갈 상태
        
        [SerializeField] private ActionStateConditionSO condition;
        [SerializeField] private ActionStateSO destinationState;
    }
}


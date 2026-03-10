using System;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "FallingConditionSO", menuName = "Scriptable Objects/State Condition/FallingConditionSO")]
    public sealed class FallingConditionSO : ActionStateConditionSO
    {
        [SerializeField] private float verticalVelocityThreshold = 0f;
        [SerializeField, Min(0f)] private float apexHysteresis = 0.1f;

        private void OnEnable()
        {
            measure = StateConditionMeasures.Polling;
        }

        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IJumpMotor jumpMotor))
            {
                return false;
            }

            // 정점 부근(속도 0 근처) 흔들림을 줄이기 위해 히스테리시스를 적용
            float fallingThreshold = verticalVelocityThreshold - apexHysteresis;
            return jumpMotor.IsJumping && jumpMotor.VerticalVelocity <= fallingThreshold;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            return base.Bind(controller, onTriggered);
        }
    }
}

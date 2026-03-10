using System;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "JumpReadyConditionSO", menuName = "Scriptable Objects/State Condition/JumpReadyConditionSO")]
    public sealed class JumpReadyConditionSO : ActionStateConditionSO
    {

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

            return jumpMotor.CanStartJump();
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            return base.Bind(controller, onTriggered);
        }
    }
}

using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "TargetSetConditionSO", menuName = "Scriptable Objects/State Condition/TargetSetConditionSO")]
    public class TargetSetConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            return false;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out Fighter fighter)
                || onTriggered == null)
                return base.Bind(controller, onTriggered);

            fighter.OnTargetChanged += Handler;

            return new StateConditionHandler(() =>
            {
                if (fighter.IsNotNull())
                    fighter.OnTargetChanged -= Handler;
            });
            
            void Handler(Health h) { if (h.IsNotNull()) onTriggered.Invoke(); }
        }
    }
}


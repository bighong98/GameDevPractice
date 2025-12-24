using System;
using TH.Attribute;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "DeadConditionSO", menuName = "Scriptable Objects/State Condition/DeadConditionSO")]
    public class DeadConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            return false;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out Health health)
                || onTriggered == null)
                return base.Bind(controller, onTriggered);
            
            health.OnDead += Handler;

            return new StateConditionHandler(() =>
            {
                if (health.IsNotNull())
                    health.OnDead -= Handler;
            });

            void Handler() { if (health.IsDead) onTriggered.Invoke(); }
        }
    }
}


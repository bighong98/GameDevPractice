using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "TargetSetConditionSO", menuName = "Scriptable Objects/State Condition/TargetSetConditionSO")]
    public class TargetSetConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IAttacker attacker)) return base.Decide(controller);

            return attacker.IsTargetValid;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out IAttacker attacker)
                || onTriggered == null)
                return base.Bind(controller, onTriggered);

            attacker.OnTargetChanged += Handler;

            return new DisposableDelegate(() =>
            {
                if (attacker.IsNotNull())
                    attacker.OnTargetChanged -= Handler;
            });
            
            void Handler(Health h) { if (h.IsNotNull()) onTriggered.Invoke(); }
        }
    }
}


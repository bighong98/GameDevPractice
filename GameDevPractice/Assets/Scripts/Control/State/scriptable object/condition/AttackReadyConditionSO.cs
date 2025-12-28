using System;
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AttackReadyConditionSO", menuName = "Scriptable Objects/State Condition/AttackReadyConditionSO")]
    public class AttackReadyConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull() || !controller.Components.TryGet(out IAttacker attacker))
                return base.Decide(controller);
            
            return attacker.IsTargetValid;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null
                || !controller.Components.TryGet(out IAttacker attacker))
                return base.Bind(controller, onTriggered);

            attacker.OnAttackReady += Handler;
            
            return new DisposableDelegate(() =>
            {
                if (attacker.IsNotNull())
                    attacker.OnAttackReady -= Handler;
            });
            
            void Handler() { if (attacker.IsTargetValid) onTriggered.Invoke();}
        }
    }
}


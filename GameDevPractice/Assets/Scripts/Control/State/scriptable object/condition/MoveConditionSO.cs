using System;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "MoveConditionSO", menuName = "Scriptable Objects/State Condition/MoveConditionSO")]
    public class MoveConditionSO : ActionStateConditionSO
    {
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null 
                                        || !controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);

            mover.OnDestinationSet += Handler;
            
            return new DisposableDelegate(() =>
            {
                if (mover.IsNotNull())
                    mover.OnDestinationSet -= Handler;
            });
            
            void Handler() { onTriggered.Invoke(); }
        }
    }
}


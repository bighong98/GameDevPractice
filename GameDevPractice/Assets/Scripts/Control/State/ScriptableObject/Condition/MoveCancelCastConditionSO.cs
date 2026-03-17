using System;
using TH.Combat;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "MoveCancelCastConditionSO", menuName = "Scriptable Objects/State Condition/Skill/MoveCancelCastConditionSO")]
    public class MoveCancelCastConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return false;
            if (!controller.Components.TryGet(out IMover _)) return false;

            if (!controller.Components.TryGet(out ISkillController skillController) || skillController == null)
            {
                return true;
            }

            return !skillController.CanMoveWhileCasting;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);

            mover.OnDestinationSet += HandleDestinationSet;

            return new DisposableDelegate(() =>
            {
                if (mover.IsNotNull())
                {
                    mover.OnDestinationSet -= HandleDestinationSet;
                }
            });

            void HandleDestinationSet()
            {
                onTriggered.Invoke();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            measure = StateConditionMeasures.EventTriggeredPolling;
            base.OnValidate();
        }
#endif
    }
}

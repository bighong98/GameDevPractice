using System;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "FollowingTargetSetConditionSO", menuName = "Scriptable Objects/State Condition/FollowingTargetSetConditionSO")]
    public class FollowingTargetSetConditionSO : ActionStateConditionSO
    {
        private void OnEnable()
        {
            measure = StateConditionMeasures.EventTriggeredPolling;
        }

        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IMover mover)) return base.Decide(controller);

            return mover.FollowingTarget.IsNotNull();
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);

            mover.OnFollowingTargetSet += HandleFollowingTargetSet;

            return new DisposableDelegate(() =>
            {
                if (mover.IsNotNull())
                    mover.OnFollowingTargetSet -= HandleFollowingTargetSet;
            });

            void HandleFollowingTargetSet(Transform target)
            {
                if (!target.IsNotNull()) return;
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

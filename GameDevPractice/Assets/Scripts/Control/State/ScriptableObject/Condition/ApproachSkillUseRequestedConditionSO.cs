using System;
using TH.Attribute;
using TH.Combat;

using TH.Resource;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(
        fileName = "ApproachSkillUseRequestedConditionSO",
        menuName = "Scriptable Objects/State Condition/Skill/ApproachSkillUseRequestedConditionSO")]
    public class ApproachSkillUseRequestedConditionSO : ActionStateConditionSO
    {
        private void OnEnable()
        {
            measure = StateConditionMeasures.EventTriggeredPolling;
        }

        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IAttacker attacker)) return base.Decide(controller);
            if (!controller.Components.TryGet(out ISkillController skillController)) return base.Decide(controller);

            return attacker.IsTargetValid &&
                   skillController.HasActiveSkill &&
                   skillController.IsActiveSkillReady;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out IAttacker attacker))
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out ISkillController skillController))
                return base.Bind(controller, onTriggered);

            skillController.OnSkillUseRequested += HandleSkillUseRequested;
            attacker.OnTargetSet += HandleTargetSet;

            return new DisposableDelegate(() =>
            {
                if (skillController.IsNotNull())
                    skillController.OnSkillUseRequested -= HandleSkillUseRequested;
                if (attacker.IsNotNull())
                    attacker.OnTargetSet -= HandleTargetSet;
            });

            void HandleSkillUseRequested(SkillTypeSO requestedSkill)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (skillController.ActiveSkill != requestedSkill) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            void HandleTargetSet(Health _)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
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

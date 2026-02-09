using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ActiveSkillReadyConditionSO", menuName = "Scriptable Objects/State Condition/Skill/ActiveSkillReadyConditionSO")]
    public class ActiveSkillReadyConditionSO : ActionStateConditionSO
    {
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

            skillController.OnSkillReady += HandleSkillReady;
            attacker.OnTargetSet += HandleTargetSet;
            attacker.OnAttackReady += HandleAttackReady;

            return new DisposableDelegate(() =>
            {
                if (skillController.IsNotNull())
                    skillController.OnSkillReady -= HandleSkillReady;
                if (attacker.IsNotNull())
                    attacker.OnTargetSet -= HandleTargetSet;
                if (attacker.IsNotNull())
                    attacker.OnAttackReady -= HandleAttackReady;
            });

            void HandleSkillReady(SkillTypeSO skill)
            {
                if (skillController.ActiveSkill != skill) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            void HandleTargetSet(Health _)
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }

            void HandleAttackReady()
            {
                if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady) return;
                if (!attacker.IsTargetValid) return;
                onTriggered.Invoke();
            }
        }
    }
}

using TH.Combat;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "MoveToTargetActionSO", menuName = "Scriptable Objects/CharacterAction/MoveToTargetActionSO")]
    public class MoveToTargetActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attackable)) return;
            if (!controller.Components.TryGet(out IMover mover)) return;
            if (!attackable.IsTargetValid) return;
            
            float requiredDistance = 0f;
            if (controller.Components.TryGet(out ISkillController skillController) && skillController.HasActiveSkill)
            {
                requiredDistance = skillController.ActiveSkillRange;
            }

            mover.SetDestination(attackable.Target.transform, requiredDistance, notify: false);
        }
    }
}


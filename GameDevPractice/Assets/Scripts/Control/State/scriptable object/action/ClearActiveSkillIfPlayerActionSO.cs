using TH.Combat;
using TH.Control;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(
        fileName = "ClearActiveSkillIfPlayerActionSO",
        menuName = "Scriptable Objects/CharacterAction/Skill/ClearActiveSkillIfPlayerActionSO")]
    public class ClearActiveSkillIfPlayerActionSO : CharacterActionSO
    {
        [SerializeField] private bool skipWhenFollowingTarget = true;

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IPlayerController _)) return;
            if (!controller.Components.TryGet(out ISkillController skillController)) return;

            if (skipWhenFollowingTarget &&
                controller.Components.TryGet(out IMover mover) &&
                mover.FollowingTarget != null)
            {
                return;
            }

            skillController.SetActiveSkill(null);
        }
    }
}

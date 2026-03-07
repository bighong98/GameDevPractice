// 활성 스킬 해제 실행 액션 에셋 스크립트
using TH.Combat;
using TH.Control;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(
        fileName = "ClearActiveSkillActionSO",
        menuName = "Scriptable Objects/CharacterAction/Skill/ClearActiveSkillActionSO")]
    // ClearActiveSkillActionSO 상태 동작 실행 액션 ScriptableObject
    public class ClearActiveSkillActionSO : CharacterActionSO
    {
        [SerializeField] private bool skipWhenFollowingTarget = true;

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IPlayerController _, alert: false)) return;
            if (!controller.Components.TryGet(out ISkillController skillController)) return;

            if (skipWhenFollowingTarget &&
                controller.Components.TryGet(out IMover mover) &&
                mover.FollowingTarget != null)
            {
                return;
            }

            skillController.TryClearActiveSkill(respectComboPreserveMarker: true);
        }
    }
}

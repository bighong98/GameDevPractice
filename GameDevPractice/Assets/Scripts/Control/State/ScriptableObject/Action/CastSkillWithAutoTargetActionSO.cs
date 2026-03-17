using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "CastSkillWithAutoTargetActionSO", menuName = "Scriptable Objects/Character Action/CastSkillWithAutoTargetActionSO")]
    public class CastSkillWithAutoTargetActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attacker)) return;
            if (!controller.Components.TryGet(out ISkillController skillController)) return;

            if (skillController.HasExecutingSkill) return;
            if (!skillController.HasActiveSkill) return;

            if (!skillController.TryGetValidAutoTargetCandidate(attacker, out Health bestTarget))
            {
                if (!skillController.TryRefreshAutoTargetCandidate(attacker) ||
                    !skillController.TryGetValidAutoTargetCandidate(attacker, out bestTarget))
                {
                    return;
                }
            }

            attacker.SetTarget(bestTarget, forceNotify: true);

            // Active Skill 사용 가능한 상태인지 최종 체크 후 공격 처리
            if (skillController.IsActiveSkillReady && !skillController.HasPendingAttack)
            {
                attacker.Attack();
            }
        }
    }
}

using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    // 상태머신에서 "현재 활성 스킬 사용"을 실행하는 액션 SO
    [CreateAssetMenu(fileName = "CastActiveSkillActionSO", menuName = "Scriptable Objects/CharacterAction/Skill/CastActiveSkillActionSO")]
    public class CastActiveSkillActionSO : CharacterActionSO
    {
        // 컨트롤러에서 IAttacker를 찾아 Attack을 호출
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out IAttacker attacker))
            {
                attacker.Attack();
            }
        }
    }
}

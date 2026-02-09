using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "CastActiveSkillActionSO", menuName = "Scriptable Objects/CharacterAction/Skill/CastActiveSkillActionSO")]
    public class CastActiveSkillActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out IAttacker attacker))
            {
                attacker.Attack();
            }
        }
    }
}

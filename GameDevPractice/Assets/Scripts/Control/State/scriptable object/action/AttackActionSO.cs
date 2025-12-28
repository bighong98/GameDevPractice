using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AttackActionSO", menuName = "Scriptable Objects/CharacterAction/AttackActionSO")]
    public class AttackActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out IFighter fighter))
            {
                fighter.Attack();
            }
        }
    }
}


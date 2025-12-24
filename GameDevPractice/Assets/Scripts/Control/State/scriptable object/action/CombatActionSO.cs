using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "CombatActionSO", menuName = "Scriptable Objects/CharacterAction/CombatActionSO")]
    public class CombatActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Fighter fighter)) return;
            
        }
    }
}


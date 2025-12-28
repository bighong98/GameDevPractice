using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "LookAtCombatTargetSO", menuName = "Scriptable Objects/CharacterAction/LookAtCombatTargetSO")]
    public class LookAtCombatTargetSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IAttacker attackable)) return;
            if (!controller.Components.TryGet(out Transform trs)) return;
            if (!attackable.IsTargetValid) return;
            
            trs.LookAt(attackable.Target.transform);
        }
    }
}


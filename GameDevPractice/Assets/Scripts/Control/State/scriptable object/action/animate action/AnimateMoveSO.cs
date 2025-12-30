using TH.Control.State;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateMoveSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateMoveSO")]
    public class AnimateMoveSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator anim) ) return;
            
            anim.CrossFade(
                stateHashName: LocomotionASSHash, 
                normalizedTransitionDuration: 0.1f, 
                AnimatorBaseLayer);
        }
    }

}

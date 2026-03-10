using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateJumpExitSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateJumpExitSO")]
    public sealed class AnimateJumpExitSO : CharacterActionSO
    {
        [SerializeField] private string preferredStateName = "Locomotion";
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private bool enableDebugLog = false;


        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning("[AnimateJumpExitSO] Animator not found on controller.", this);
                }
                return;
            }

            if (!TryResolveStateHash(animator, out int stateHash))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning($"[AnimateJumpExitSO] Failed to resolve locomotion state '{preferredStateName}'.", this);
                }
                return;
            }

            animator.CrossFade(stateHash, transitionDuration, AnimatorBaseLayer);
            if (enableDebugLog)
            {
                Logg.Log($"[AnimateJumpExitSO] CrossFade -> stateHash={stateHash}, duration={transitionDuration:0.000}", Logg.LoggingMode.InProgress, this);
            }
        }

        private bool TryResolveStateHash(Animator animator, out int stateHash)
        {
            return TryGetStateHash(animator, preferredStateName, out stateHash);
        }

        private static bool TryGetStateHash(Animator animator, string stateName, out int stateHash)
        {
            stateHash = 0;
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (TryGetStateHashInternal(animator, stateName, out stateHash))
            {
                return true;
            }

            if (stateName.Contains("."))
            {
                return false;
            }

            string layerQualifiedStateName = $"{animator.GetLayerName(AnimatorBaseLayer)}.{stateName}";
            return TryGetStateHashInternal(animator, layerQualifiedStateName, out stateHash);
        }

        private static bool TryGetStateHashInternal(Animator animator, string stateName, out int stateHash)
        {
            stateHash = 0;
            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(AnimatorBaseLayer, hash))
            {
                return false;
            }

            stateHash = hash;
            return true;
        }
    }
}

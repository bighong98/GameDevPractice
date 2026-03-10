using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateJumpDownSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateJumpDownSO")]
    public sealed class AnimateJumpDownSO : CharacterActionSO
    {
        [SerializeField] private string preferredStateName = "Jump";
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private bool skipWhenAlreadyInState = true;
        [SerializeField] private bool enableDebugLog = false;


        [Header("Motion Time")]
        [SerializeField] private bool setMotionTimeOnEnter = true;
        [SerializeField] private string jumpMotionTimeParameterName = "JumpMotionTime";
        [SerializeField, Min(1)] private int totalFrameCount = 60;

        [SerializeField, Min(0)] private int downHoldFrame = 34;


        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning("[AnimateJumpDownSO] Animator not found on controller.", this);
                }
                return;
            }

            if (!TryResolveStateHash(animator, out int stateHash))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning($"[AnimateJumpDownSO] Failed to resolve jump state '{preferredStateName}'.", this);
                }
                return;
            }

            if (skipWhenAlreadyInState && IsCurrentOrNextState(animator, stateHash))
            {
                if (enableDebugLog)
                {
                    Logg.Log($"[AnimateJumpDownSO] Skip because animator is already in jump state(hash={stateHash}).", Logg.LoggingMode.InProgress, this);
                }
                return;
            }

            float downHoldNormalizedTime = FrameToNormalized(downHoldFrame);
            if (setMotionTimeOnEnter && TryGetFloatParameterHash(animator, jumpMotionTimeParameterName, out int motionTimeHash))
            {
                animator.SetFloat(motionTimeHash, downHoldNormalizedTime);
            }

            animator.CrossFade(stateHash, transitionDuration, AnimatorBaseLayer, downHoldNormalizedTime);
            if (enableDebugLog)
            {
                Logg.Log($"[AnimateJumpDownSO] CrossFade -> stateHash={stateHash}, downHoldFrame={downHoldFrame}, normalized={downHoldNormalizedTime:0.000}", Logg.LoggingMode.InProgress, this);
            }
        }

        private float FrameToNormalized(int frame)
        {
            int safeTotalFrameCount = Mathf.Max(1, totalFrameCount);
            int clampedFrame = Mathf.Clamp(frame, 0, safeTotalFrameCount);
            return clampedFrame / (float)safeTotalFrameCount;
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

        private static bool TryGetFloatParameterHash(Animator animator, string parameterName, out int parameterHash)
        {
            parameterHash = 0;
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            int hash = Animator.StringToHash(parameterName);
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == hash)
                {
                    parameterHash = hash;
                    return true;
                }
            }

            return false;
        }

        private static bool IsCurrentOrNextState(Animator animator, int stateHash)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer);
            if (currentState.shortNameHash == stateHash || currentState.fullPathHash == stateHash)
            {
                return true;
            }

            if (!animator.IsInTransition(AnimatorBaseLayer))
            {
                return false;
            }

            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(AnimatorBaseLayer);
            return nextState.shortNameHash == stateHash || nextState.fullPathHash == stateHash;
        }
    }
}


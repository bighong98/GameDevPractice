using System;
using TH.Control.Movement;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "LandedConditionSO", menuName = "Scriptable Objects/State Condition/LandedConditionSO")]
    public sealed class LandedConditionSO : ActionStateConditionSO
    {
        [Header("Jump Exit Wait")]
        [SerializeField] private bool waitForJumpExitCompletion = true;
        [SerializeField] private string jumpMotionTimeParameterName = "JumpMotionTime";
        [SerializeField, Range(0f, 1f)] private float requiredMotionTime = 0.99f;
        [SerializeField, Min(0f)] private float forceCompleteDelay = 0.15f;


        private void OnEnable()
        {
            measure = StateConditionMeasures.Polling;
        }

        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IJumpMotor jumpMotor))
            {
                return false;
            }

            bool landed = jumpMotor.IsGrounded && !jumpMotor.IsJumping;
            if (!landed || !waitForJumpExitCompletion)
            {
                return landed;
            }

            if (!controller.Components.TryGet(out Animator animator))
            {
                return landed;
            }

            if (!TryGetFloatParameterHash(animator, jumpMotionTimeParameterName, out int motionTimeHash))
            {
                return landed;
            }

            if (animator.GetFloat(motionTimeHash) >= requiredMotionTime)
            {
                return true;
            }

            return (Time.time - jumpMotor.LastGroundedTime) >= forceCompleteDelay;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            return base.Bind(controller, onTriggered);
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
    }
}


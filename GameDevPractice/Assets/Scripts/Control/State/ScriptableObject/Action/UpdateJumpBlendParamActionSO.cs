using System.Collections.Generic;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "UpdateJumpBlendParamActionSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/UpdateJumpBlendParamActionSO")]
    public sealed class UpdateJumpBlendParamActionSO : CharacterActionSO
    {
        private const int ForcedStartFrame = 0;

        [Header("Motion Time (FullJump 60 Frames)")]
        [SerializeField] private string jumpMotionTimeParameterName = "JumpMotionTime";
        [SerializeField, Min(1)] private int totalFrameCount = 60;
        [SerializeField, Min(0)] private int startEndFrame = 33;
        [SerializeField, Min(0)] private int downHoldFrame = 34;
        [SerializeField, Min(0)] private int exitStartFrame = 35;
        [SerializeField, Min(0.01f)] private float exitDuration = 0.2f;
        [SerializeField, Min(0f)] private float apexDeadZone = 0.1f;
        [SerializeField] private bool enableDebugLog = false;

        [System.NonSerialized] private readonly Dictionary<int, ExitPlaybackState> exitPlaybackByAnimatorId = new();
        [System.NonSerialized] private readonly Dictionary<int, float> jumpStartVerticalSpeedByAnimatorId = new();
        [System.NonSerialized] private readonly Dictionary<int, bool> jumpActiveByAnimatorId = new();
        [System.NonSerialized] private readonly Dictionary<int, bool> downHoldActiveByAnimatorId = new();
        [System.NonSerialized] private readonly Dictionary<int, bool> wasGroundedByAnimatorId = new();



        private struct ExitPlaybackState
        {
            public bool active;
            public float elapsed;
        }

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator))
            {
                return;
            }

            if (!controller.Components.TryGet(out IJumpMotor jumpMotor))
            {
                return;
            }

            if (!TryGetFloatParameterHash(animator, jumpMotionTimeParameterName, out int jumpMotionTimeHash))
            {
                return;
            }

            int animatorId = animator.GetInstanceID();
            wasGroundedByAnimatorId.TryGetValue(animatorId, out bool wasGrounded);
            bool jumpStartEdge = wasGrounded && !jumpMotor.IsGrounded && jumpMotor.IsJumping;
            wasGroundedByAnimatorId[animatorId] = jumpMotor.IsGrounded;

            UpdateJumpMotionTime(animator, jumpMotor, jumpMotionTimeHash, jumpStartEdge);
        }

        private void UpdateJumpMotionTime(Animator animator, IJumpMotor jumpMotor, int motionTimeHash, bool jumpStartEdge)
        {
            int animatorId = animator.GetInstanceID();
            exitPlaybackByAnimatorId.TryGetValue(animatorId, out ExitPlaybackState exitState);
            jumpActiveByAnimatorId.TryGetValue(animatorId, out bool wasJumping);
            downHoldActiveByAnimatorId.TryGetValue(animatorId, out bool wasDownHold);
            float downHoldNormalized = FrameToNormalized(downHoldFrame);

            float nextMotionTime;
            if (jumpMotor.IsJumping)
            {
                exitState.active = false;
                exitState.elapsed = 0f;

                if (jumpStartEdge || !wasJumping)
                {
                    jumpStartVerticalSpeedByAnimatorId[animatorId] = Mathf.Max(0.01f, jumpMotor.VerticalVelocity);
                    jumpActiveByAnimatorId[animatorId] = true;
                    downHoldActiveByAnimatorId[animatorId] = false;

                    nextMotionTime = FrameToNormalized(ForcedStartFrame);
                    exitPlaybackByAnimatorId[animatorId] = exitState;
                    animator.SetFloat(motionTimeHash, Mathf.Clamp01(nextMotionTime));

                    if (enableDebugLog)
                    {
                        string trigger = jumpStartEdge ? "grounded_edge" : "jump_flag";
                        Logg.Log($"[UpdateJumpBlendParamActionSO] JumpStart reset -> animatorId={animatorId}, trigger={trigger}, startFrame={ForcedStartFrame}, verticalVelocity={jumpMotor.VerticalVelocity:0.000}", Logg.LoggingMode.InProgress, this);
                    }
                    return;
                }

                if (!jumpStartVerticalSpeedByAnimatorId.TryGetValue(animatorId, out float jumpStartVerticalSpeed) ||
                    jumpStartVerticalSpeed <= Mathf.Epsilon ||
                    jumpMotor.VerticalVelocity > jumpStartVerticalSpeed)
                {
                    jumpStartVerticalSpeed = Mathf.Max(0.01f, jumpMotor.VerticalVelocity);
                    jumpStartVerticalSpeedByAnimatorId[animatorId] = jumpStartVerticalSpeed;
                }

                if (jumpMotor.VerticalVelocity > apexDeadZone)
                {
                    float ascentProgress = 1f - Mathf.Clamp01(jumpMotor.VerticalVelocity / jumpStartVerticalSpeed);
                    nextMotionTime = Mathf.Lerp(FrameToNormalized(ForcedStartFrame), FrameToNormalized(startEndFrame), ascentProgress);
                    downHoldActiveByAnimatorId[animatorId] = false;
                }
                else
                {
                    nextMotionTime = downHoldNormalized;
                    if (!wasDownHold)
                    {
                        downHoldActiveByAnimatorId[animatorId] = true;
                        if (enableDebugLog)
                        {
                            Logg.Log($"[UpdateJumpBlendParamActionSO] Enter down-hold -> animatorId={animatorId}, downHoldFrame={downHoldFrame}, verticalVelocity={jumpMotor.VerticalVelocity:0.000}", Logg.LoggingMode.InProgress, this);
                        }
                    }
                }

                jumpActiveByAnimatorId[animatorId] = true;
            }
            else if (jumpMotor.IsGrounded)
            {
                jumpStartVerticalSpeedByAnimatorId.Remove(animatorId);
                jumpActiveByAnimatorId[animatorId] = false;
                downHoldActiveByAnimatorId.Remove(animatorId);

                if (!exitState.active)
                {
                    exitState.active = true;
                    exitState.elapsed = 0f;

                    if (enableDebugLog)
                    {
                        Logg.Log($"[UpdateJumpBlendParamActionSO] Start exit playback -> animatorId={animatorId}, exitStartFrame={exitStartFrame}, exitDuration={exitDuration:0.000}", Logg.LoggingMode.InProgress, this);
                    }
                }

                float exitStartNormalized = FrameToNormalized(exitStartFrame);
                float exitEndNormalized = FrameToNormalized(totalFrameCount);
                float t = exitDuration > Mathf.Epsilon
                    ? Mathf.Clamp01(exitState.elapsed / exitDuration)
                    : 1f;

                nextMotionTime = Mathf.Lerp(exitStartNormalized, exitEndNormalized, t);
                exitState.elapsed += Time.deltaTime;
            }
            else
            {
                jumpActiveByAnimatorId[animatorId] = false;
                downHoldActiveByAnimatorId.Remove(animatorId);
                float currentMotionTime = animator.GetFloat(motionTimeHash);
                nextMotionTime = Mathf.Min(currentMotionTime, downHoldNormalized);

                if (enableDebugLog && currentMotionTime > downHoldNormalized)
                {
                    Logg.Log($"[UpdateJumpBlendParamActionSO] Clamp airborne motionTime -> animatorId={animatorId}, from={currentMotionTime:0.000}, to={downHoldNormalized:0.000}", Logg.LoggingMode.InProgress, this);
                }
            }

            if (!jumpMotor.IsGrounded)
            {
                nextMotionTime = Mathf.Min(nextMotionTime, downHoldNormalized);
            }

            exitPlaybackByAnimatorId[animatorId] = exitState;
            animator.SetFloat(motionTimeHash, Mathf.Clamp01(nextMotionTime));
        }

        private float FrameToNormalized(int frame)
        {
            int safeTotalFrameCount = Mathf.Max(1, totalFrameCount);
            int clampedFrame = Mathf.Clamp(frame, 0, safeTotalFrameCount);
            return clampedFrame / (float)safeTotalFrameCount;
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
